using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ResourceDesk.App;
public sealed class ActionDialog : Window
{
    private readonly CheckBox? _stop;
    public bool StopRequested => _stop?.IsChecked == true;
    public ActionDialog(string heading, string explanation, string action, string? stopLabel)
    {
        Title = heading; Width = 470; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Display.Brush("#151C24"); Foreground = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = heading, FontSize = 23, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = explanation, FontSize = 13, Foreground = Display.Brush("#A7B8C9"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,14,0,8) });
        if (stopLabel != null)
        {
            _stop = new CheckBox { Content = stopLabel, IsChecked = true }; panel.Children.Add(_stop);
            panel.Children.Add(new TextBlock { Text = "Stopping interrupts this integration in active chats. Other integrations keep running.", FontSize = 11, Foreground = Display.Brush("#D5B786"), TextWrapping = TextWrapping.Wrap });
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,24,0,0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0,0,8,0) };
        var apply = new Button { Content = action, Style = (Style)FindResource("PrimaryButton"), IsDefault = true };
        cancel.Click += (_, _) => DialogResult = false; apply.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(apply); panel.Children.Add(actions); Content = panel;
    }
}
public sealed class SettingsDialog : Window
{
    public string? SelectedPath { get; private set; }
    public SettingsDialog(string current)
    {
        Title = "Resource Desk settings"; Width = 540; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Display.Brush("#151C24");
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = "Your configuration", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Global Codex MCP settings", Foreground = Display.Brush("#008AC9"), Margin = new Thickness(0,12,0,8) });
        panel.Children.Add(new TextBox { Text = current, IsReadOnly = true, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Resource Desk edits this file only. Project overrides and plugin-managed settings may differ. Built-in helpers are shown read-only. Backups are stored beside the configuration and may contain credentials; keep them private.", TextWrapping = TextWrapping.Wrap, Foreground = Display.Brush("#A7B8C9"), FontSize = 12, Margin = new Thickness(0,16,0,0) });
        panel.Children.Add(new TextBlock { Text = "Memory uses binary units (MiB / GiB). Codex RAM counts private resident pages, while committed memory can include paged-out pages. Monitoring runs locally every five seconds; no data is sent anywhere.", TextWrapping = TextWrapping.Wrap, Foreground = Display.Brush("#A7B8C9"), FontSize = 12, Margin = new Thickness(0,12,0,0) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,22,0,0) };
        var browse = new Button { Content = "Choose config…", Margin = new Thickness(0,0,8,0) };
        browse.Click += (_, _) => { var picker = new OpenFileDialog { Filter = "TOML configuration|*.toml", Title = "Choose the Codex configuration to manage", CheckFileExists = true }; if (picker.ShowDialog(this) == true) { SelectedPath = picker.FileName; DialogResult = true; } };
        var close = new Button { Content = "Done", IsCancel = true }; close.Click += (_, _) => DialogResult = false;
        actions.Children.Add(browse); actions.Children.Add(close); panel.Children.Add(actions); Content = panel;
    }
}
