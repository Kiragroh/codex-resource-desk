using ResourceDesk.Core;

namespace ResourceDesk.Tests;

public static class ProcessTests
{
    public static int Run()
    {
        int count = 0;
        var epoch = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        const string unityPath = @"C:\fixture\.codex\mcp-servers\unity-mcp-server\src\index.js";
        var unity = new ServerConfig("unity", "Unity", true, @"C:\Program Files\nodejs\node.exe", [unityPath], false);
        var config = new ConfigSnapshot("fixture", "hash", [unity], []);
        ProcessInfo P(int id, int parent, int second, string path, string cmd = "") =>
            new(id, parent, epoch.AddSeconds(second), Path.GetFileName(path), cmd, path, 900, 200, 100, 0, 1);
        var root = P(10, 1, 0, @"C:\Program Files\WindowsApps\OpenAI.Codex_1.0_x64\app\Codex.exe");
        var server = P(20, 10, 1, @"C:\Program Files\nodejs\node.exe", $"node.exe \"{unityPath}\"");
        ProcessSnapshot S(params ProcessInfo[] processes) => new(epoch.AddHours(1), new(10000, 1000, 100, 10000, 10, 10), processes, 1, []);
        void Test(string name, Action action)
        {
            try { action(); count++; }
            catch (Exception ex) { throw new Exception("Process fixture failed: " + name, ex); }
        }
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        ComponentInfo Unity(ResourceSnapshot r) => r.Components.Single(c => c.ServerKeys.Contains("unity"));

        Test("private resident totals and unique ownership", () =>
        {
            var child = P(21, 20, 2, @"C:\runtime\helper.exe");
            var result = ProcessAttribution.Build(S(root, server, child), config);
            Check(result.CodexRamBytes == 300 && result.CodexCommitBytes == 600, "Private working set and commit must remain separate.");
            Check(Unity(result).Count == 2, "Matched descendants should inherit server ownership.");
            Check(result.Components.SelectMany(c => c.Processes).Select(p => p.Id).Distinct().Count() == 3, "All attributed processes must occur exactly once.");
            Check(result.Components.Sum(c => c.Count) == result.CodexProcesses.Count, "No duplicate assignments.");
        });
        Test("unowned matching server excluded", () =>
        {
            var unrelated = server with { Id = 90, ParentId = 1 };
            var result = ProcessAttribution.Build(S(root, unrelated), config);
            Check(Unity(result).Count == 0 && result.CodexProcesses.Count == 1, "A matching path alone cannot establish Codex ownership.");
        });
        Test("missing ancestor and reused parent", () =>
        {
            Check(Unity(ProcessAttribution.Build(S(server), config)).Count == 0, "Missing root must not be inferred.");
            var laterRoot = root with { StartedUtc = epoch.AddMinutes(1) };
            Check(Unity(ProcessAttribution.Build(S(laterRoot, server), config)).Count == 0, "Newer parent identity must invalidate ancestry.");
        });
        Test("cycles and duplicate pids fail closed", () =>
        {
            var a = server with { ParentId = 21, StartedUtc = epoch };
            var b = P(21, 20, 0, @"C:\runtime\helper.exe");
            Check(ProcessAttribution.Build(S(root, a, b), config).CodexProcesses.Count == 1, "Cyclic ancestry must fail closed.");
            Check(Unity(ProcessAttribution.Build(S(root, root with { StartedUtc = epoch.AddSeconds(2) }, server), config)).Count == 0, "Duplicate PID inventory must fail closed.");
        });
        Test("disabled but still running", () =>
        {
            var result = ProcessAttribution.Build(S(root, server), config with { Servers = [unity with { Enabled = false }] });
            Check(Unity(result).Enabled == false && Unity(result).Count == 1, "Disabled config must not hide a running process.");
            Check(Unity(ProcessAttribution.Build(S(root), config)).Count == 0, "Configured integrations remain visible without a process.");
        });
        Test("Hostinger family and Unity remain disjoint", () =>
        {
            var family = Enumerable.Range(1, 9).Select(i => new ServerConfig("hostinger_" + i, "Hostinger " + i, false, "npx", ["--package=hostinger-api-mcp@latest", "hostinger-test-mcp"], false)).ToArray();
            var npm = P(30, 10, 2, @"C:\node\node.exe", @"node C:\cache\_npx\abc\node_modules\hostinger-api-mcp\src\servers\hosting.js");
            var result = ProcessAttribution.Build(S(root, server, npm), config with { Servers = family.Append(unity).ToArray() });
            var hostinger = result.Components.Single(c => c.Key == "hostinger");
            Check(hostinger.ServerKeys.Count == 9 && hostinger.Count == 1 && hostinger.Enabled == false, "Nine Hostinger entries form one disabled running group.");
            Check(Unity(result).Count == 1, "Unity remains a separate integration.");
        });
        Test("ambiguous entry is read only", () =>
        {
            var duplicate = unity with { Key = "other-unity", DisplayName = "Other Unity" };
            var result = ProcessAttribution.Build(S(root, server), config with { Servers = [unity, duplicate] });
            Check(result.Components.Single(c => c.Key == "ambiguous").Count == 1, "Overlapping configs must not receive duplicate process assignment.");
            Check(!Unity(result).CanControl, "Ambiguous integration must be read only.");
        });
        Test("builtin and core cannot be controlled", () =>
        {
            var repl = P(40, 10, 2, @"C:\node\node.exe", @"node C:\codex\node_repl.js");
            var malicious = new ServerConfig("fake-core", "Fake", true, root.ExecutablePath, [], false);
            var result = ProcessAttribution.Build(S(root, repl), config with { Servers = [unity, malicious] });
            Check(result.Components.Single(c => c.Key == "core").Count == 1, "Core cannot be remapped by config.");
            Check(result.Components.Single(c => c.Key == "node-repl").CanControl == false, "Built-in runtime must be protected.");
            Check(!ProcessController.CanStopSelection(result.Components.Single(c => c.Key == "core") with { CanControl = true }, root, root, result.Components.Single(c => c.Key == "core")), "Fabricating a control flag cannot authorize core stop.");
        });
        Test("stop identity and ownership gate", () =>
        {
            var selected = Unity(ProcessAttribution.Build(S(root, server), config));
            Check(ProcessController.CanStopSelection(selected, server, server, selected), "Exact owned identity should pass policy.");
            Check(!ProcessController.CanStopSelection(selected, server, server with { StartedUtc = epoch.AddSeconds(30) }, selected), "PID reuse must fail.");
            Check(!ProcessController.CanStopSelection(selected, server, server with { ExecutablePath = @"C:\other\node.exe" }, selected), "Executable changes must fail.");
            Check(!ProcessController.CanStopSelection(selected, server, server, selected with { CanControl = false }), "Fresh protection must override old permission.");
            Check(!ProcessController.CanStopSelection(selected, server, server, selected with { Processes = [] }), "Fresh lost ownership must fail.");
            Check(!ProcessController.CanStopSelection(selected with { ServerKeys = ["forged"] }, server, server, selected), "Forged server identity must fail.");
        });
        Test("other verified work retained, unrelated dev server excluded", () =>
        {
            var shell = P(50, 10, 3, @"C:\Windows\System32\cmd.exe", "cmd /c npm run dev");
            var dev = P(51, 1, 3, @"C:\node\node.exe", "node vite");
            var result = ProcessAttribution.Build(S(root, shell, dev), config);
            Check(result.CodexProcesses.Count == 2 && result.Components.Single(c => c.Key == "other").Count == 1, "Count genuine Codex children but not unrelated development processes.");
        });
        Test("prefix collision is not an entry match", () =>
        {
            var near = server with { CommandLine = $"node.exe \"{unityPath}.backup\"" };
            Check(Unity(ProcessAttribution.Build(S(root, near), config)).Count == 0, "Loose path substrings must not assign unrelated entries.");
        });
        Test("searching or passing an entry path does not launch that server", () =>
        {
            var shell = P(61, 10, 3, @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", $"powershell.exe -Command Select-String \"{unityPath}\"");
            var node = server with { Id = 62, CommandLine = $"node.exe C:\\fixture\\unrelated.js \"{unityPath}\"" };
            var preloaded = server with { Id = 63, CommandLine = $"node.exe --require \"{unityPath}\" C:\\fixture\\unrelated.js" };
            var host = new ServerConfig("hostinger-test", "Hostinger", false, "npx", ["--package=hostinger-api-mcp@latest", "hostinger-test-mcp"], false);
            var packageSearch = shell with { Id = 64, CommandLine = "powershell.exe -Command Write-Output --package=hostinger-api-mcp@latest" };
            var packageInput = node with { Id = 65, CommandLine = "node.exe C:\\fixture\\unrelated.js --package=hostinger-api-mcp@latest" };
            var result = ProcessAttribution.Build(S(root, shell, node, preloaded, packageSearch, packageInput), config with { Servers = [unity, host] });
            Check(Unity(result).Count == 0 && result.Components.Single(c => c.Key == "hostinger").Count == 0, "A shell search, input path, preloader or package argument must never become a stoppable MCP.");
            var misleadingConfig = config with { Servers = [unity with { Args = [@"C:\fixture\unrelated.js", unityPath] }] };
            Check(Unity(ProcessAttribution.Build(S(root, server), misleadingConfig)).Count == 0, "Configured input arguments must not be mistaken for its executable entrypoint.");
        });
        Test("actual builtin entry shapes and quoted npx wrapper", () =>
        {
            var cua = P(70, 10, 3, @"C:\fixture\.codex\runtimes\cua_node\node.exe", "node.exe ./server.mjs");
            var repl = P(71, 10, 3, @"C:\fixture\node_repl.exe", "node_repl.exe");
            var proxy = P(72, 10, 3, @"C:\node\node.exe", @"node.exe C:\fixture\servers\mcp-proxy.mjs");
            var wrapper = P(73, 10, 3, @"C:\Windows\System32\cmd.exe", "cmd.exe /d /s /c \"npx --package=hostinger-api-mcp@latest hostinger-hosting-mcp\"");
            var host = new ServerConfig("hostinger-test", "Hostinger", false, "npx", ["--package=hostinger-api-mcp@latest", "hostinger-hosting-mcp"], false);
            var result = ProcessAttribution.Build(S(root, cua, repl, proxy, wrapper), config with { Servers = [unity, host] });
            Check(result.Components.Single(c => c.Key == "computer-use").Count == 1, "CUA bundled node must be recognized.");
            Check(result.Components.Single(c => c.Key == "node-repl").Count == 1, "Native Node REPL must be recognized.");
            Check(result.Components.Single(c => c.Key == "mcp-proxy").Count == 1, "MJS MCP proxy must be recognized.");
            Check(result.Components.Single(c => c.Key == "hostinger").Count == 1, "Known quoted npx launcher must be recognized.");
        });
        return count;
    }
}
