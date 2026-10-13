using System.Diagnostics;
using System.IO;

namespace ResourceDesk.App;

internal static class TrayChecks
{
    // Explicit test mode: only the app-created temporary config is changed. No server is launched or stopped.
    public static async Task Run(MainWindow window, TrayHost tray, string fixture)
    {
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static async Task Until(Func<bool> predicate)
        {
            var time = Stopwatch.StartNew();
            while (!predicate()) { if (time.Elapsed > TimeSpan.FromSeconds(18)) throw new TimeoutException("Background discovery did not update."); await Task.Delay(100); }
        }
        await Until(() => window.CompletedSamples > 0);
        Require(tray.IsVisible, "Tray icon missing");
        window.Close();
        Require(!window.IsVisible && tray.IsVisible, "Close did not hide to tray");
        int samples = window.CompletedSamples;
        File.WriteAllText(fixture, "[mcp_servers.discovery_fixture]\ncommand='node'\nargs=['C:/fixture/never-launch.js']\nenabled=true\n");
        await Until(() => window.CompletedSamples > samples && window.HasIntegration("server:discovery_fixture", true));
        await window.ToggleMonitoringAsync();
        Require(window.IsMonitoringPaused && window.MonitoringSummary.Contains("paused"), "Pause state missing");
        samples = window.CompletedSamples;
        await Task.Delay(5500);
        Require(window.CompletedSamples == samples, "Paused monitoring kept polling");
        File.WriteAllText(fixture, "[mcp_servers.discovery_fixture]\ncommand='node'\nenabled=false\n");
        await window.ToggleMonitoringAsync();
        await Until(() => window.HasIntegration("server:discovery_fixture", false));
        Require(!window.IsMonitoringPaused, "Resume failed");
        File.WriteAllText(fixture, "# Fixture removed\n");
        await Until(() => !window.HasIntegration("server:discovery_fixture"));
        tray.Open(); Require(window.IsVisible, "Tray open did not restore window");
        Console.WriteLine("PASS: tray visibility, close/hide, hidden polling, automatic add/disable/remove, pause, resume, reopen (9 lifecycle checks).");
        tray.Exit();
        Require(!tray.IsVisible, "Tray icon was not disposed on exit");
    }
}
