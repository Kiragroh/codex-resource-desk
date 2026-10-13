using System.Text;

namespace ResourceDesk.Core;

public static class ProcessAttribution
{
    private static readonly string[] Colors = ["#008AC9", "#8064A2", "#6BA8D0", "#AE93CB", "#927AAE", "#5291B5"];
    private sealed record Group(string Key, string Name, ServerConfig[] Servers);

    public static ResourceSnapshot Build(ProcessSnapshot snapshot, ConfigSnapshot config)
    {
        var map = snapshot.Processes.GroupBy(p => p.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        var roots = map.Values.Where(IsCodexRoot).Select(p => p.Id).ToHashSet();
        bool Owned(ProcessInfo process)
        {
            var seen = new HashSet<int>();
            var p = process;
            while (seen.Add(p.Id))
            {
                if (roots.Contains(p.Id)) return true;
                if (!map.TryGetValue(p.ParentId, out var parent) || !ValidParent(p, parent)) return false;
                p = parent;
            }
            return false;
        }
        var owned = map.Values.Where(Owned).ToArray();
        var groups = config.Servers.GroupBy(s => IsHostinger(s) ? "hostinger" : s.Key.Equals("node_repl", StringComparison.OrdinalIgnoreCase) ? "node-repl" : "server:" + s.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Group(g.Key, g.Key == "hostinger" ? "Hostinger" : g.First().DisplayName, g.ToArray())).ToArray();
        var assignments = new Dictionary<int, string>();
        var ambiguous = new HashSet<string>();
        foreach (var p in owned)
        {
            string? special = Builtin(p);
            if (special != null) { assignments[p.Id] = special; continue; }
            if (roots.Contains(p.Id)) { assignments[p.Id] = "core"; continue; }
            var matches = groups.Where(g => g.Servers.Any(s => !s.IsRemote && Matches(p, s))).ToArray();
            if (matches.Length == 1) assignments[p.Id] = matches[0].Key;
            else if (matches.Length > 1)
            {
                assignments[p.Id] = "ambiguous";
                foreach (var match in matches) ambiguous.Add(match.Key);
            }
        }
        string Resolve(ProcessInfo p)
        {
            if (assignments.TryGetValue(p.Id, out var direct)) return direct;
            var visited = new HashSet<int>();
            var at = p;
            while (visited.Add(at.Id) && map.TryGetValue(at.ParentId, out var parent) && ValidParent(at, parent))
            {
                if (assignments.TryGetValue(parent.Id, out string? inherited))
                    return inherited == "core" ? (IsBundled(p) ? "core" : "other") : inherited;
                at = parent;
            }
            return "other";
        }
        // Resolve against direct anchors only, so the result is independent of inventory order.
        var final = owned.ToDictionary(p => p.Id, Resolve);
        ProcessInfo[] Members(string key) => owned.Where(p => final[p.Id] == key).OrderByDescending(p => p.PrivateWorkingSetBytes).ToArray();
        var components = new List<ComponentInfo>();
        int color = 0;
        foreach (var g in groups)
        {
            var members = Members(g.Key);
            bool? enabled = g.Servers.All(s => s.Enabled) ? true : g.Servers.All(s => !s.Enabled) ? false : null;
            bool controllable = g.Key != "node-repl" && g.Servers.All(s => !s.IsRemote && !s.IsProtected) && !ambiguous.Contains(g.Key);
            components.Add(new(g.Key, g.Name, g.Servers.Length > 1 ? $"{g.Servers.Length} configured servers" : "Configured MCP server", Colors[color++ % Colors.Length],
                g.Servers.Select(s => s.Key).ToArray(), enabled, controllable, members,
                ambiguous.Contains(g.Key) ? "Matching entries overlap; controls are disabled until attribution is unambiguous." :
                g.Servers.Any(s => s.IsProtected) ? "Protected integration; read-only." :
                g.Servers.All(s => s.IsRemote) ? "Remote integration; no local server process is attributed." :
                "Only processes with verified Codex ancestry and a matching server entry, plus their descendants, are attributed. Configuration and current runtime are independent."));
        }
        AddBuiltin("core", "Codex core", "Application and bundled runtime", "#76859B");
        AddBuiltin("computer-use", "Browser & computer use", "Built-in browser helpers", "#008AC9");
        AddBuiltin("node-repl", "Node REPL", "Built-in execution runtime", "#8064A2");
        AddBuiltin("mcp-proxy", "MCP proxies", "Built-in transport helpers", "#A494C2");
        AddBuiltin("other", "Other Codex work", "Other verified descendants", "#7294B2");
        AddBuiltin("ambiguous", "Ambiguous integrations", "Overlapping server matches", "#B4A0CF");
        return new(snapshot, components, owned);

        void AddBuiltin(string key, string name, string subtitle, string shade)
        {
            if (components.Any(c => c.Key == key)) return;
            var members = Members(key);
            if (members.Length > 0 || key == "core")
                components.Add(new(key, name, subtitle, shade, [], null, false, members, "Read-only. These processes cannot be stopped or disabled from Resource Desk."));
        }
    }

    public static bool SameIdentity(ProcessInfo expected, ProcessInfo current) => expected.Id == current.Id &&
        expected.StartedUtc == current.StartedUtc && !string.IsNullOrWhiteSpace(expected.ExecutablePath) &&
        Normalize(expected.ExecutablePath) == Normalize(current.ExecutablePath);

    internal static bool ValidParent(ProcessInfo child, ProcessInfo parent) => child.Id != parent.Id && parent.StartedUtc <= child.StartedUtc && child.SessionId == parent.SessionId;
    internal static bool IsCodexRoot(ProcessInfo p)
    {
        string path = Normalize(p.ExecutablePath);
        string file = path.Split('/').Last();
        return (file == "codex.exe" || file == "chatgpt.exe") && IsTrustedPath(path);
    }
    private static bool IsTrustedPath(string path) => path.Contains("/openai/codex/") || path.Contains("/windowsapps/openai.codex_");
    private static bool IsBundled(ProcessInfo p) => IsTrustedPath(Normalize(p.ExecutablePath));
    private static bool IsHostinger(ServerConfig s) => s.Key.StartsWith("hostinger", StringComparison.OrdinalIgnoreCase) ||
        s.Args.Any(a => a.Contains("hostinger-api-mcp", StringComparison.OrdinalIgnoreCase));

    private static string? Builtin(ProcessInfo p)
    {
        var tokens = CommandTokens(p.CommandLine);
        string path = Normalize(p.ExecutablePath);
        bool Entry(string value) => tokens.Any(t => t.Split('/').Last().Equals(value, StringComparison.OrdinalIgnoreCase));
        if (path.EndsWith("/node_repl.exe") || Entry("node_repl.js") || Entry("node-repl.js") || Entry("node_repl.mjs") || tokens.Contains("node_repl")) return "node-repl";
        if (Entry("mcp-proxy") || Entry("mcp-proxy.exe") || Entry("mcp-proxy.mjs") || Entry("mcp_proxy") || Entry("mcp_proxy.exe")) return "mcp-proxy";
        if (tokens.Any(t => t.Contains("/computer-use/") || t.Contains("/unified-computer-use/") || t.Contains("/cua-server/")) ||
            path.Contains("/codex/browser/") || path.Contains("/codex/runtimes/cua_node/") || path.Contains("/.codex/runtimes/cua_node/") || (path.EndsWith("/chrome.exe") && tokens.Any(t => t.StartsWith("--remote-debugging-")))) return "computer-use";
        return null;
    }

    private static bool Matches(ProcessInfo p, ServerConfig server)
    {
        var tokens = InvocationTokens(p);
        if (tokens.Length == 0) return false;
        string actual = Normalize(p.ExecutablePath).Split('/').Last();
        string invocation = tokens[0].Split('/').Last();
        bool cmdWrapper = actual is "cmd.exe" or "cmd";
        bool node = actual == "node.exe" || (cmdWrapper && invocation is "node" or "node.exe");
        string? entry = ScriptEntry(tokens);
        bool npxLauncher = (cmdWrapper && invocation is "npx" or "npx.cmd") ||
            (node && entry != null && entry.Split('/').Last() is "npx-cli.js" or "npm-cli.js");
        // A package name merely printed, searched, or supplied as a data argument is not a server.
        if (IsHostinger(server))
        {
            if (node && entry?.Contains("/node_modules/hostinger-api-mcp/") == true) return true;
            return npxLauncher && tokens.Any(t => t == "hostinger-api-mcp" || t.StartsWith("hostinger-api-mcp@") ||
                t.StartsWith("--package=hostinger-api-mcp@") || t == "--package=hostinger-api-mcp");
        }
        var command = Normalize(server.Command);
        var generic = new[] { "node", "node.exe", "python", "python.exe", "python3", "python3.exe", "uv", "uv.exe", "uvx", "uvx.exe", "npx", "npx.cmd", "npm", "npm.cmd", "cmd.exe", "powershell.exe", "pwsh.exe", "dotnet", "dotnet.exe" };
        if (command.Contains('/') && !generic.Contains(command.Split('/').Last()) && Normalize(p.ExecutablePath) == command) return true;
        string expectedExecutable = command.Split('/').Last();
        bool sameRuntime = command.Contains('/') ? Normalize(p.ExecutablePath) == command :
            actual.TrimEndExtension() == expectedExecutable.TrimEndExtension();
        if (cmdWrapper && expectedExecutable is "node" or "node.exe") sameRuntime = node &&
            (!command.Contains('/') || tokens[0] == command);
        string? configuredEntry = ScriptEntry(new[] { command }.Concat(server.Args.Select(Normalize)).ToArray());
        if (sameRuntime && entry != null && configuredEntry != null && IsEntryPath(configuredEntry) && entry == configuredEntry) return true;
        // Require an explicit package operand, not a generic runtime name or a loose substring.
        string cmdName = command.Split('/').Last();
        if ((cmdName is "npx" or "npx.cmd" && (npxLauncher || node)) ||
            (cmdName is "uvx" or "uvx.exe" && actual is "uvx" or "uvx.exe"))
        {
            string? package = server.Args.FirstOrDefault(a => a.StartsWith("--package=", StringComparison.OrdinalIgnoreCase));
            package = package?[10..] ?? server.Args.FirstOrDefault(a => !a.StartsWith('-') &&
                System.Text.RegularExpressions.Regex.IsMatch(a, @"^(@[a-zA-Z0-9_.-]+/)?[a-zA-Z0-9_.-]+(@[a-zA-Z0-9_.^~*-]+)?$"));
            if (package != null)
            {
                string normalized = Normalize(package);
                int version = normalized.LastIndexOf('@');
                string bare = version > 0 ? normalized[..version] : normalized;
                if (npxLauncher && (tokens.Contains(normalized) || tokens.Contains("--package=" + normalized))) return true;
                if (node && bare.Length > 2 && entry?.Contains("/node_modules/" + bare + "/") == true) return true;
            }
        }
        return false;
    }

    private static bool IsEntryPath(string value) => value.Contains('/') && !value.Contains("://") &&
        (value.EndsWith(".js") || value.EndsWith(".mjs") || value.EndsWith(".cjs") || value.EndsWith(".py") || value.EndsWith(".ps1") || value.EndsWith(".exe") || value.EndsWith(".dll"));
    private static string Normalize(string value) => value.Trim('"', '\'').Replace('\\', '/').ToLowerInvariant();
    private static string TrimEndExtension(this string value) => value.EndsWith(".exe") || value.EndsWith(".cmd") ? value[..^4] : value;

    private static string? ScriptEntry(string[] tokens)
    {
        for (int i = 1; i < tokens.Length; i++)
        {
            string t = tokens[i];
            if (t is "-e" or "--eval" or "-p" or "--print" or "-command" or "-c" || t.StartsWith("--eval=") || t.StartsWith("--print=")) return null;
            if (t is "-r" or "--require" or "--import" or "--loader" or "--experimental-loader") { i++; continue; }
            if (t is "--" or "-file") continue;
            if (t.StartsWith('-')) continue;
            return t;
        }
        return null;
    }

    private static string[] InvocationTokens(ProcessInfo p)
    {
        var tokens = Tokenize(p.CommandLine).Select(Normalize).ToArray();
        if (Normalize(p.ExecutablePath).Split('/').Last() is not ("cmd.exe" or "cmd")) return tokens;
        int start = Array.IndexOf(tokens, "/c");
        if (start < 0 || start + 1 >= tokens.Length) return [];
        var nested = Tokenize(tokens[start + 1]).Select(Normalize).Concat(tokens.Skip(start + 2)).ToArray();
        return nested.Length > 0 && nested[0].Split('/').Last() is "npx" or "npx.cmd" or "node" or "node.exe" ? nested : [];
    }

    private static string[] CommandTokens(string command)
    {
        var tokens = Tokenize(command).Select(Normalize).ToList();
        // Windows cmd wraps npx in a quoted /c command. Unwrap only this known launcher form.
        if (tokens.Count > 2 && tokens[0].Split('/').Last() is "cmd" or "cmd.exe")
        {
            int commandIndex = tokens.IndexOf("/c");
            if (commandIndex >= 0 && commandIndex + 1 < tokens.Count)
            {
                var nested = Tokenize(tokens[commandIndex + 1]).Select(Normalize).ToArray();
                if (nested.Length > 0 && nested[0].Split('/').Last() is "npx" or "npx.cmd" or "node" or "node.exe") tokens.AddRange(nested);
            }
        }
        return tokens.ToArray();
    }

    // Windows command-line quoting, including runs of backslashes immediately before quotes.
    internal static string[] Tokenize(string command)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        bool quoted = false, active = false;
        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];
            if (c == '\\')
            {
                int count = 0;
                while (i < command.Length && command[i] == '\\') { count++; i++; }
                if (i < command.Length && command[i] == '"')
                {
                    token.Append('\\', count / 2);
                    if (count % 2 == 1) token.Append('"'); else quoted = !quoted;
                }
                else { token.Append('\\', count); i--; }
                active = true;
            }
            else if (c == '"') { quoted = !quoted; active = true; }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (active) { result.Add(token.ToString()); token.Clear(); active = false; }
            }
            else { token.Append(c); active = true; }
        }
        if (active) result.Add(token.ToString());
        return result.ToArray();
    }
}
