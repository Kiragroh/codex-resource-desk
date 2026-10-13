using System.IO;
using System.Windows;

namespace ResourceDesk.App;
public partial class App : Application
{
    private bool _handlingError;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        DispatcherUnhandledException += (_, a) =>
        {
            a.Handled = true;
            if (_handlingError) return;
            _handlingError = true;
            Console.Error.WriteLine(a.Exception.GetType().Name + ": " + a.Exception.StackTrace);
            if (!e.Args.Contains("--screenshot")) MessageBox.Show("Resource Desk encountered an unexpected error and will close. No automatic retry will run.\n\n" + a.Exception.GetType().Name, "Resource Desk", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        };
        var window = new MainWindow(e.Args);
        MainWindow = window;
        window.Show();
    }
}
