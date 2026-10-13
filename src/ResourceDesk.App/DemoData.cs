using ResourceDesk.Core;
namespace ResourceDesk.App;
internal static class DemoData
{
    public static (ConfigSnapshot, ResourceSnapshot) Create()
    {
        const long MiB = 1048576; const long GiB = 1073741824;
        var now = DateTime.UtcNow;
        var servers = new[] { new ServerConfig("unity", "Unity", true, "", [], false), new ServerConfig("hostinger-hosting", "Hostinger", false, "", [], false) };
        var cfg = new ConfigSnapshot("demo — no real configuration", "demo", servers, []);
        var groups = new List<ComponentInfo>(); int pid = 1000;
        void Add(string key, string name, string color, int count, long ram, bool? enabled, bool control, string[] keys, string explanation)
        {
            var procs = Enumerable.Range(0,count).Select(_ => new ProcessInfo(pid++, 0, now.AddMinutes(-12), key == "core" ? "Codex.exe" : "node.exe", "", "", ram / Math.Max(1,count), ram * 2 / Math.Max(1,count), ram / Math.Max(1,count), 0, 1)).ToArray();
            groups.Add(new(key, name, "", color, keys, enabled, control, procs, explanation));
        }
        Add("core", "Codex core", "#8064A2", 22, 2400*MiB, null, false, [], "The Codex application, renderers and main service. These shared processes stay under Codex's control.");
        Add("computer-use", "Browser & computer use", "#008AC9", 31, 1600*MiB, null, false, [], "Shared browser and automation helpers. Read-only to avoid interrupting active work.");
        Add("server:unity", "Unity", "#008AC9", 14, 640*MiB, true, true, ["unity"], "Your Unity MCP integration. Configuration and running processes are tracked independently.");
        Add("node-repl", "Node REPL", "#8064A2", 18, 145*MiB, true, false, [], "Execution helpers used by Codex. These are displayed without a stop control.");
        Add("mcp-proxy", "MCP proxies", "#A494C2", 4, 112*MiB, null, false, [], "Transport helpers shared by Codex tools.");
        Add("hostinger", "Hostinger", "#927AAE", 0, 0, false, true, ["hostinger-hosting"], "Disabled integrations remain visible here. Enable when you need them; Codex starts them on its next connection.");
        var procs = groups.SelectMany(g => g.Processes).ToArray();
        var sample = new ProcessSnapshot(now, new(64*GiB, 43*GiB, 39*GiB, 128*GiB, 1*GiB, 2*GiB), procs, 27, []);
        return (cfg, new(sample, groups, procs));
    }
}
