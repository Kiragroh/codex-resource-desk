using System.IO;
using System.Windows;
using System.Security.Principal;

namespace ResourceDesk.App;
public partial class App : Application
{
    private bool _handlingError;
    private TrayHost? _tray;
    private Mutex? _instance;
    private EventWaitHandle? _activate;
    private RegisteredWaitHandle? _activationWait;
    private bool _ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        DispatcherUnhandledException += (_, a) =>
        {
            a.Handled = true;
            if (_handlingError) return;
            _handlingError = true;
            Console.Error.WriteLine(a.Exception.GetType().Name + ": " + a.Exception.StackTrace);
            if (!e.Args.Contains("--screenshot") && !e.Args.Contains("--lifecycle-check")) MessageBox.Show("Resource Desk encountered an unexpected error and will close. No automatic retry will run.\n\n" + a.Exception.GetType().Name, "Resource Desk", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        };
        bool check = e.Args.Contains("--lifecycle-check");
        bool standalone = e.Args.Contains("--screenshot") || e.Args.Contains("--demo") || check;
        if (!standalone)
        {
            string key = "Local\\ResourceDesk." + WindowsIdentity.GetCurrent().User!.Value;
            _activate = new EventWaitHandle(false, EventResetMode.AutoReset, key + ".activate");
            _instance = new Mutex(true, key, out _ownsInstance);
            if (!_ownsInstance) { _activate.Set(); Shutdown(); return; }
        }
        string? fixture = null;
        var args = e.Args;
        if (check)
        {
            fixture = Path.Combine(Path.GetTempPath(), "ResourceDesk.TrayCheck." + Guid.NewGuid().ToString("N") + ".toml");
            File.WriteAllText(fixture, "# Disposable discovery fixture\n");
            args = ["--config", fixture];
        }
        var window = new MainWindow(args);
        MainWindow = window;
        bool useTray = !e.Args.Contains("--screenshot") && !e.Args.Contains("--no-tray");
        if (useTray) _tray = new TrayHost(window);
        else window.Closed += (_, _) => Shutdown();
        if (_activate != null)
            _activationWait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) =>
            {
                if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(() => { if (_tray != null) _tray.Open(); else { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); } });
            }, null, Timeout.Infinite, false);
        if (e.Args.Contains("--tray") && useTray)
        {
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Loaded += (_, _) => Dispatcher.BeginInvoke(() => { window.Hide(); window.ShowInTaskbar = true; window.ShowActivated = true; });
        }
        if (check) window.Loaded += async (_, _) =>
        {
            try { await TrayChecks.Run(window, _tray!, fixture!); }
            catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); Shutdown(1); }
            finally { if (File.Exists(fixture)) File.Delete(fixture); }
        };
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _activationWait?.Unregister(null); _activate?.Dispose(); _tray?.Dispose();
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose(); base.OnExit(e);
    }
}
