using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ResourceDesk.App;

internal sealed class TrayHost : IDisposable
{
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _notify;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _status = new("Reading resource data…") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _pause = new("Pause monitoring");
    private readonly System.Drawing.Icon _icon;
    private bool _exiting, _explained, _disposed;
    public bool IsVisible => !_disposed && _notify.Visible;

    public TrayHost(MainWindow window)
    {
        _window = window;
        using var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ResourceDesk.ico")).Stream;
        using var loaded = new System.Drawing.Icon(resource);
        _icon = (System.Drawing.Icon)loaded.Clone();
        _menu.Items.Add(_status);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Open Resource Desk", null, (_, _) => Dispatch(Open));
        _menu.Items.Add("Refresh now", null, (_, _) => Dispatch(async () => await _window.RefreshMonitoringAsync()));
        _pause.Click += (_, _) => Dispatch(async () => await _window.ToggleMonitoringAsync());
        _menu.Items.Add(_pause);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => Dispatch(Exit));
        _notify = new Forms.NotifyIcon { Icon = _icon, Text = "Resource Desk · starting", ContextMenuStrip = _menu, Visible = true };
        _notify.DoubleClick += (_, _) => Dispatch(Open);
        _window.Closing += WindowClosing;
        _window.MonitoringChanged += UpdateStatus;
        UpdateStatus();
    }
    private void Dispatch(Action action) { if (!_disposed && !Application.Current.Dispatcher.HasShutdownStarted) Application.Current.Dispatcher.BeginInvoke(action); }
    public void Open()
    {
        if (_disposed) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        foreach (Window owned in _window.OwnedWindows) if (owned.IsVisible) owned.Activate();
    }
    public void Hide() { if (!_disposed) _window.Hide(); }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true; Hide();
        if (!_explained)
        {
            _explained = true;
            _notify.ShowBalloonTip(2500, "Resource Desk is in the tray", "Monitoring keeps its current state. Double-click the icon to open; choose Exit to quit.", Forms.ToolTipIcon.Info);
        }
    }
    private void UpdateStatus()
    {
        if (_disposed) return;
        var text = _window.MonitoringSummary;
        _notify.Text = text.Length > 127 ? text[..127] : text;
        _status.Text = text;
        _pause.Text = _window.IsMonitoringPaused ? "Resume monitoring" : "Pause monitoring";
    }
    public void Exit()
    {
        if (_window.IsActionBusy || _window.OwnedWindows.Cast<Window>().Any(w => w.IsVisible))
        {
            Open();
            _notify.ShowBalloonTip(2000, "Action in progress", "Finish the current action or dialog, then choose Exit again.", Forms.ToolTipIcon.Info);
            return;
        }
        _exiting = true;
        Dispose();
        Application.Current.Shutdown();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _exiting = true;
        _window.Closing -= WindowClosing;
        _window.MonitoringChanged -= UpdateStatus;
        _notify.Visible = false; _notify.Dispose(); _menu.Dispose(); _icon.Dispose();
    }
}
