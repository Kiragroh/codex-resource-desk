using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ResourceDesk.Core;

namespace ResourceDesk.App;
public partial class MainWindow : Window
{
    private readonly DeskViewModel _vm = new();
    private readonly ConfigStore _store = new();
    private readonly WindowsSampler _sampler = new();
    private readonly SemaphoreSlim _gate = new(1);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly List<double> _history = [];
    private readonly Dictionary<string, ComponentRow> _rows = [];
    private ConfigSnapshot? _config;
    private ResourceSnapshot? _snapshot;
    private ConfigChange? _lastChange;
    private string _configPath;
    private string _filter = "All";
    private bool _paused, _busy, _closed;
    private bool _fresh;
    private int _configGeneration;
    private readonly bool _demo;
    private readonly string? _screenshot;

    public MainWindow(string[] args)
    {
        InitializeComponent(); DataContext = _vm;
        _configPath = Arg(args, "--config") ?? ConfigStore.DefaultPath;
        _demo = args.Contains("--demo");
        _screenshot = Arg(args, "--screenshot");
        if (_demo) { Title += " · DEMO"; Notice("DEMO · Simulated sample data. All controls are read-only."); }
        _timer.Tick += async (_, _) => { if (!_paused) await RefreshAsync(); };
        Loaded += async (_, _) =>
        {
            await RefreshAsync(); _timer.Start();
            if (args.Contains("--ui-check") && _demo) ValidateDemoView();
            if (_screenshot != null)
            {
                await Task.Delay(500); UpdateLayout();
                var content = (FrameworkElement)Content;
                var target = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                target.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_screenshot))!);
                using (var file = File.Create(_screenshot)) encoder.Save(file);
                Close();
            }
        };
        Closed += (_, _) => { _closed = true; _timer.Stop(); };
    }
    private static string? Arg(string[] args, string key) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    private void ValidateDemoView()
    {
        void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException("UI check: " + name); }
        Require(_vm.Rows.Any(r => r.Key == "hostinger" && r.Data.Enabled == false), "disabled integration retained");
        Require(_vm.Rows.All(r => !r.CanAct) && !_vm.CanStopSelected, "demo cannot mutate");
        Require(Math.Abs(_vm.SystemPercent - 100 * 21d / 64) < .01, "physical memory fraction");
        _filter = "Disabled"; ApplySnapshot();
        Require(_vm.Rows.Count > 0 && _vm.Rows.All(r => r.Data.Enabled == false) && _vm.Selected != null && _vm.Rows.Contains(_vm.Selected), "selection follows disabled filter");
        _filter = "Running"; ApplySnapshot();
        Require(_vm.Rows.All(r => r.Data.Count > 0) && _vm.Selected != null && _vm.Rows.Contains(_vm.Selected), "selection follows running filter");
        SearchBox.Text = "no-such-integration"; ApplySnapshot();
        Require(_vm.Rows.Count == 0 && _vm.Selected == null && !_vm.CanStopSelected, "empty search clears inspector");
        SearchBox.Text = ""; _filter = "All"; ApplySnapshot(); FocusComponent("server:unity");
        var configured = _snapshot!.Components.First(c => c.Key == "hostinger");
        Require(new ComponentRow(configured with { CanControl = false }).Status.StartsWith("Disabled"), "read-only retains config state");
        Console.WriteLine("PASS: 7 native UI checks (simulated data, no mutations).");
    }
    private void Notice(string message, bool error = false)
    {
        NoticeText.Text = message;
        NoticeBorder.Background = Display.Brush(error ? "#40282B" : "#1D2C3B");
        NoticeBorder.Visibility = Visibility.Visible;
    }
    private async Task RefreshAsync()
    {
        if (_closed || !await _gate.WaitAsync(0)) return;
        try
        {
            var generation = _configGeneration;
            var path = _configPath;
            var result = await Task.Run(() =>
            {
                if (_demo) return DemoData.Create();
                var cfg = _store.Load(path);
                return (cfg, ProcessAttribution.Build(_sampler.Capture(), cfg));
            });
            if (_closed || generation != _configGeneration) return;
            _config = result.Item1; _snapshot = result.Item2;
            _fresh = true;
            ApplySnapshot();
            var warnings = _config.Warnings.Concat(_snapshot.Raw.Warnings).ToArray();
            if (warnings.Length > 0) Notice(string.Join("  ", warnings.Take(3)));
            LiveLabel.Text = _demo ? "● DEMO DATA" : _paused ? "Ⅱ PAUSED" : "● LIVE · 5s";
        }
        catch (Exception ex)
        {
            _fresh = false;
            LiveLabel.Text = "! STALE / ERROR";
            _config = null; _vm.CanStopSelected = false;
            foreach (var row in _rows.Values) row.Update(row.Data, _snapshot?.CodexRamBytes ?? 0, true);
            _vm.Changed(); Notice(SafeError(ex), true);
        }
        finally { _gate.Release(); }
    }
    private static string SafeError(Exception ex) => ex switch
    {
        FileNotFoundException => "No Codex config was found. Use Settings to select your config.toml. No changes were made.",
        UnauthorizedAccessException => "Access was denied. Resource Desk does not request elevation or change permissions.",
        ConfigurationConflictException => "An external config edit raced this switch. Its contents were preserved in an adjacent .resourcedesk.*.bak file. Check that backup and reconcile config.toml before switching again.",
        _ => "The action could not complete (" + ex.GetType().Name + "). Refresh and try again. Your current configuration may have changed; no automatic retry will run."
    };
    private void ApplySnapshot()
    {
        if (_snapshot == null) return;
        var mem = _snapshot.Raw.Memory;
        _vm.SystemUsed = Display.Size(mem.TotalBytes - mem.AvailableBytes);
        _vm.SystemTotal = "/ " + Display.Size(mem.TotalBytes);
        _vm.SystemFree = Display.Size(mem.AvailableBytes) + " available · " + (mem.TotalBytes == 0 ? 0 : mem.AvailableBytes * 100 / mem.TotalBytes) + "% free";
        _vm.SystemTip = $"System physical RAM: {Display.Size(mem.TotalBytes - mem.AvailableBytes)} used, {Display.Size(mem.AvailableBytes)} available.\nSystem commit: {Display.Size(mem.CommitBytes)} of {Display.Size(mem.CommitLimitBytes)}.\nKernel pools: {Display.Size(mem.KernelPagedBytes + mem.KernelNonPagedBytes)}.";
        _vm.SystemPercent = mem.TotalBytes <= 0 ? 0 : 100 * (1d - mem.AvailableBytes / (double)mem.TotalBytes);
        _vm.CodexRam = Display.Size(_snapshot.CodexRamBytes);
        _vm.CodexCommit = Display.Size(_snapshot.CodexCommitBytes) + " committed · includes paged-out memory";
        _vm.ProcessCount = _snapshot.CodexProcesses.Count.ToString("N0");
        _vm.RunningTools = _snapshot.Components.Count(c => c.ServerKeys.Count > 0 && c.Count > 0) + " integrations with live processes";
        _vm.DisabledTools = _snapshot.Components.Count(c => c.ServerKeys.Count > 0 && c.Enabled == false) + " integrations disabled in config";
        _history.Add(_snapshot.CodexRamBytes); if (_history.Count > 36) _history.RemoveAt(0);
        var max = Math.Max(1, _history.Max());
        _vm.HistoryPoints = new PointCollection(_history.Select((n, i) => new Point(i * 270d / 35, 21 - n / max * 18)));
        var self = Process.GetCurrentProcess();
        _vm.Footer = $"{(_demo ? "Demo sample" : "Updated " + _snapshot.Raw.CapturedUtc.ToLocalTime().ToString("HH:mm:ss"))} · scan {_snapshot.Raw.ScanMilliseconds:0} ms · Desk {Display.Size(self.WorkingSet64)} working set";
        var selected = _vm.Selected?.Key;
        foreach (var component in _snapshot.Components)
        {
            if (!_rows.TryGetValue(component.Key, out var row)) _rows[component.Key] = row = new ComponentRow(component);
            row.Update(component, _snapshot.CodexRamBytes, _busy || _demo);
        }
        foreach (var key in _rows.Keys.Where(k => !_snapshot.Components.Any(c => c.Key == k)).ToArray()) _rows.Remove(key);
        UpdateFilter();
        _vm.Selected = _vm.Rows.FirstOrDefault(r => r.Key == selected) ?? _vm.Rows.FirstOrDefault(r => r.Key.Contains("unity", StringComparison.OrdinalIgnoreCase)) ?? _vm.Rows.FirstOrDefault();
        UpdateSelection(); DrawBreakdown(); _vm.Changed();
    }
    private void UpdateFilter()
    {
        if (SearchBox == null) return;
        var search = SearchBox.Text.Trim();
        var desired = _rows.Values.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) && (_filter != "Running" || r.Data.Count > 0) && (_filter != "Disabled" || r.Data.Enabled == false)).OrderByDescending(r => r.Data.CanControl).ThenByDescending(r => r.Data.RamBytes).ThenBy(r => r.Name).ToArray();
        for (var i = _vm.Rows.Count - 1; i >= 0; i--) if (!desired.Contains(_vm.Rows[i])) _vm.Rows.RemoveAt(i);
        for (var i = 0; i < desired.Length; i++)
        {
            var index = _vm.Rows.IndexOf(desired[i]);
            if (index < 0) _vm.Rows.Insert(i, desired[i]); else if (index != i) _vm.Rows.Move(index, i);
        }
    }
    private void DrawBreakdown()
    {
        BreakdownBar.Children.Clear(); BreakdownBar.ColumnDefinitions.Clear(); BreakdownLegend.Children.Clear();
        if (_snapshot == null) return;
        var items = _snapshot.Components.Where(c => c.RamBytes > 0).OrderByDescending(c => c.RamBytes).ToArray();
        var index = 0;
        foreach (var component in items)
        {
            BreakdownBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1, component.RamBytes), GridUnitType.Star) });
            var button = new Button { Background = Display.Brush(component.Color), BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(0,0,2,0), ToolTip = $"{component.Name} · {Display.Size(component.RamBytes)} RAM · {component.Count} processes", MinWidth = 2 };
            button.Click += (_, _) => FocusComponent(component.Key);
            System.Windows.Automation.AutomationProperties.SetName(button, component.Name + " memory segment");
            Grid.SetColumn(button, index++); BreakdownBar.Children.Add(button);
            var legend = new Button { Content = new TextBlock { Text = "● " + component.Name, Foreground = Display.Brush(component.Color), FontSize = 10 }, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0,2,16,2), ToolTip = button.ToolTip };
            legend.Click += (_, _) => FocusComponent(component.Key); BreakdownLegend.Children.Add(legend);
        }
    }
    private void FocusComponent(string key)
    {
        _filter = "All"; SearchBox.Text = ""; UpdateFilter(); UpdateFilterButtons();
        if (_rows.TryGetValue(key, out var row)) { _vm.Selected = row; _vm.Changed(); ComponentsList.SelectedItem = row; ComponentsList.ScrollIntoView(row); UpdateSelection(); }
    }
    private void UpdateSelection()
    {
        _vm.CanStopSelected = !_demo && !_busy && _config != null && _vm.Selected?.Data.CanControl == true && _vm.Selected.Data.Count > 0;
        _vm.CanUndo = !_demo && !_busy && _lastChange != null;
        _vm.Changed();
    }
    private void Selection_Changed(object sender, SelectionChangedEventArgs e) { if (DataContext != null) { _vm.Selected = ComponentsList.SelectedItem as ComponentRow; UpdateSelection(); } }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (DataContext != null) UpdateFilter(); }
    private void UpdateFilterButtons()
    {
        foreach (var button in new[] { AllFilter, RunningFilter, DisabledFilter }) button.Background = Display.Brush((string)button.Tag == _filter ? "#315779" : "#202C39");
    }
    private void Filter_Click(object sender, RoutedEventArgs e) { _filter = (string)((Button)sender).Tag; UpdateFilter(); UpdateFilterButtons(); }
    private async void Pause_Click(object sender, RoutedEventArgs e) { _paused = !_paused; PauseButton.Content = _paused ? "Resume" : "Pause"; LiveLabel.Text = !_fresh ? "! STALE / ERROR" : _paused ? "Ⅱ PAUSED" : _demo ? "● DEMO DATA" : "● LIVE · 5s"; if (!_paused) await RefreshAsync(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_busy || _demo || _config == null || ((FrameworkElement)sender).DataContext is not ComponentRow row || !row.Data.CanControl) return;
        var enable = row.Data.Enabled != true;
        var stop = false;
        if (!enable)
        {
            var dialog = new ActionDialog("Disable " + row.Name + "?", "Codex will no longer start this integration from this global configuration. Existing chats may keep their current connection until reloaded.", "Disable integration", row.Data.Count > 0 ? $"Also stop {row.Data.Count} verified processes now" : null) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            stop = dialog.StopRequested;
        }
        var config = _config; var component = row.Data;
        await RunMutation(async () =>
        {
            var change = await Task.Run(() => _store.SetEnabled(config.Path, component.ServerKeys, enable, config.Hash));
            _lastChange = change;
            var text = enable ? $"{row.Name} enabled. Codex starts it on its next connection. If needed, start a new chat or restart Codex after current work finishes." : $"{row.Name} disabled. A backup was saved beside your config.";
            if (stop)
            {
                var stopped = await Task.Run(() => new ProcessController().Stop(component, change.Snapshot));
                text += $" Stopped {stopped.Stopped}; already exited {stopped.AlreadyExited}.";
                if (stopped.Failures.Count > 0) text += " Some processes could not be verified or stopped. Refresh the inspector.";
            }
            Notice(text);
        });
    }
    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanStopSelected || _vm.Selected == null || _config == null) return;
        var component = _vm.Selected.Data; var config = _config;
        var dialog = new ActionDialog("Stop " + component.Name + "?", $"This disconnects the integration from active Codex chats. Only its {component.Count} verified processes will be targeted. Its configuration stays {(component.Enabled == true ? "enabled, so Codex can start it again" : "unchanged") }.", "Stop processes", null) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await RunMutation(async () => { var result = await Task.Run(() => new ProcessController().Stop(component, config)); Notice($"Stopped {result.Stopped}; already exited {result.AlreadyExited}; could not verify or stop {result.Failures.Count}. Refreshing live state."); });
    }
    private async Task RunMutation(Func<Task> action)
    {
        if (_busy) return; _busy = true; UpdateSelection();
        foreach (var row in _rows.Values) row.Update(row.Data, _snapshot?.CodexRamBytes ?? 0, true);
        await _gate.WaitAsync();
        try { await action(); } catch (Exception ex) { Notice(SafeError(ex), true); }
        finally { _busy = false; _gate.Release(); UpdateSelection(); }
        await RefreshAsync();
    }
    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastChange == null || _busy || _demo) return;
        var change = _lastChange;
        await RunMutation(async () => { await Task.Run(() => _store.Undo(change)); _lastChange = null; Notice("Last configuration switch undone. Stopped processes are not restarted. Later external edits are never overwritten."); });
    }
    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new SettingsDialog(_configPath) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedPath != null)
        {
            _configGeneration++; _configPath = dialog.SelectedPath; _config = null; _snapshot = null; _fresh = false;
            _lastChange = null; _rows.Clear(); _vm.Rows.Clear(); _vm.Selected = null; UpdateSelection();
            LiveLabel.Text = "READING CONFIG";
            await _gate.WaitAsync(); _gate.Release();
            await RefreshAsync();
        }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot == null) return;
        var dialog = new SaveFileDialog { FileName = "resource-desk-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json", Filter = "JSON snapshot|*.json", Title = "Save a sanitized resource snapshot" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var data = new { app = "Resource Desk", version = "0.1.1", demo = _demo, capturedUtc = _snapshot.Raw.CapturedUtc, memory = _snapshot.Raw.Memory, codexPrivateResidentBytes = _snapshot.CodexRamBytes, codexCommittedBytes = _snapshot.CodexCommitBytes, processes = _snapshot.CodexProcesses.Count, components = _snapshot.Components.Select(c => new { name = c.Name, enabled = c.Enabled, processes = c.Count, privateResidentBytes = c.RamBytes, committedBytes = c.CommitBytes }), note = "Contains integration names and aggregate metrics only. No credentials, command lines, executable paths, hostnames or document titles." };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); Notice("Snapshot saved. Review integration names before sharing publicly.");
        }
        catch (Exception ex) { Notice(SafeError(ex), true); }
    }
}
