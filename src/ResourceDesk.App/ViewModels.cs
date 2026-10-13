using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using ResourceDesk.Core;

namespace ResourceDesk.App;
public class Notify : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Changed(string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public static class Display
{
    public static string Size(long n) => n >= 1073741824 ? $"{n / 1073741824d:0.0} GiB" : $"{n / 1048576d:0} MiB";
    public static Brush Brush(string color) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }
}
public sealed record ProcessRow(string Name, string Ram, string Detail);
public sealed class ComponentRow : Notify
{
    public ComponentInfo Data { get; private set; }
    private long _total; private bool _busy;
    public ComponentRow(ComponentInfo data) => Data = data;
    public string Key => Data.Key;
    public string Name => Data.Name;
    public string Initial => Data.Key == "hostinger" ? "H" : Data.Name[..1].ToUpperInvariant();
    public Brush Tint => Display.Brush(Data.Color);
    public bool IsOn => Data.Enabled == true;
    public bool CanAct => Data.CanControl && !_busy;
    public Visibility SwitchVisibility => Data.CanControl ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ReadOnlyVisibility => Data.CanControl ? Visibility.Collapsed : Visibility.Visible;
    public string ReadOnlyLabel => Data.ServerKeys.Count > 0 ? "READ ONLY" : "MANAGED";
    public string Status => Data.ServerKeys.Count == 0 ? (Data.Count > 0 ? "Managed by Codex · running" : "Managed by Codex") : Data.Enabled switch
    {
        false => Data.Count > 0 ? "Disabled · processes still running" : "Disabled · no local processes",
        true => Data.Count > 0 ? "Enabled · running" : "Enabled · no local processes",
        _ => "Mixed settings · inspect before switching"
    };
    public string Ram => Display.Size(Data.RamBytes);
    public string Commit => Display.Size(Data.CommitBytes);
    public string Count => $"{Data.Count} process{(Data.Count == 1 ? "" : "es")}";
    public double RamPercent => _total <= 0 ? 0 : Math.Clamp(Data.RamBytes / (double)_total * 100, 0, 100);
    public string ToggleTip => $"{(IsOn ? "Disable" : "Enable")} {Name}. Configuration and running processes are separate.";
    public string Hover => $"{Name}\n{Status}\n{Count} · {Ram} private resident RAM\n{Commit} private committed memory\n\n{Data.Explanation}";
    public string Explanation => Data.Explanation;
    public string EmptyText => Data.Count == 0 ? "No attributed local process. A configured remote service does not need a dedicated local process." : Data.Count > 100 ? "Showing the first 100 processes." : "";
    public string ControlNote => Data.CanControl ? "Switches apply to this global config. Codex may need a new connection or restart. Stop disconnects current chats." : "Read-only: " + Data.Explanation;
    public IReadOnlyList<ProcessRow> ProcessRows => Data.Processes.OrderByDescending(p => p.PrivateWorkingSetBytes).Take(100).Select(p => new ProcessRow(p.Name, Display.Size(p.PrivateWorkingSetBytes), $"PID {p.Id}  ·  {Math.Max(0, (DateTime.UtcNow - p.StartedUtc).TotalMinutes):0} min old")).ToArray();
    public void Update(ComponentInfo data, long total, bool busy) { Data = data; _total = total; _busy = busy; Changed(); }
}
public sealed class DeskViewModel : Notify
{
    public ObservableCollection<ComponentRow> Rows { get; } = [];
    public ComponentRow? Selected { get; set; }
    public string SystemUsed { get; set; } = "—";
    public string SystemTotal { get; set; } = "";
    public string SystemFree { get; set; } = "Reading system memory…";
    public string SystemTip { get; set; } = "Physical memory across the entire system.";
    public double SystemPercent { get; set; }
    public string CodexRam { get; set; } = "—";
    public string CodexCommit { get; set; } = "Waiting for a verified sample";
    public string ProcessCount { get; set; } = "—";
    public string RunningTools { get; set; } = "";
    public string DisabledTools { get; set; } = "";
    public string Footer { get; set; } = "Starting local read-only monitoring…";
    public PointCollection HistoryPoints { get; set; } = [];
    public bool CanUndo { get; set; }
    public bool CanStopSelected { get; set; }
}
