using System.Text;
using ResourceDesk.Core;

namespace ResourceDesk.Tests;

public static class ConfigTests
{
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "ResourceDesk.ConfigTests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var count = 0;
        try
        {
            void Test(Action<string> action)
            {
                var directory = Path.Combine(root, count.ToString());
                Directory.CreateDirectory(directory);
                action(Path.Combine(directory, "config.toml"));
                count++;
            }
            Test(path =>
            {
                const string source = "# config\r\nmodel = 'example'\r\n[mcp_servers.unity] # keep\r\ncommand = 'node'\r\nenabled  = true # flag\r\nargs = ['x']\r\n[mcp_servers.unity.env]\r\nTOKEN = 'fixture-secret'\r\n";
                File.WriteAllText(path, source, new UTF8Encoding(true));
                var before = File.ReadAllBytes(path);
                var store = new ConfigStore();
                var initial = store.Load(path);
                var change = store.SetEnabled(path, ["unity"], false, initial.Hash);
                Equal(source.Replace("= true #", "= false #"), File.ReadAllText(path));
                Assert(!change.Snapshot.Servers.Single().Enabled, "Flag was not disabled");
                Assert(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "BOM lost");
                Assert(before.SequenceEqual(File.ReadAllBytes(change.BackupPath)), "Backup differs");
                store.Undo(change);
                Assert(before.SequenceEqual(File.ReadAllBytes(path)), "Undo is not byte exact");
                Refused(() => store.Undo(change));
            });
            Test(path =>
            {
                const string source = "[\"mcp_servers\".'quoted.name'] # keep\ncommand='node'\n'enabled'=false # preserve\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                var change = store.SetEnabled(path, ["quoted.name"], true, store.Load(path).Hash);
                Equal(source.Replace("=false #", "=true #"), File.ReadAllText(path));
                Assert(change.Snapshot.Servers.Single().Enabled, "Quoted key failed");
            });
            Test(path =>
            {
                const string source = "[mcp_servers.example] # header\ncommand='node'\n[mcp_servers.example.env]\nKEY='value'\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                store.SetEnabled(path, ["example"], false, store.Load(path).Hash);
                Equal(source.Replace("# header\n", "# header\nenabled = false\n"), File.ReadAllText(path));
            });
            Test(path =>
            {
                const string source = "[mcp_servers.empty] # no newline";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                store.SetEnabled(path, ["empty"], false, store.Load(path).Hash);
                Equal(source + "\nenabled = false\n", File.ReadAllText(path));
            });
            Test(path =>
            {
                const string source = "description = '''\n[mcp_servers.fake]\nenabled = true\n'''\n[mcp_servers.real]\ncommand='node'\nenabled=true\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                Equal("real", store.Load(path).Servers.Single().Key);
                Refused(() => store.SetEnabled(path, ["fake"], false, store.Load(path).Hash));
                Equal(source, File.ReadAllText(path));
                store.SetEnabled(path, ["real"], false, store.Load(path).Hash);
                Equal(source.Replace("enabled=true", "enabled=false"), File.ReadAllText(path));
            });
            Test(path =>
            {
                const string source = "[mcp_servers.real]\ncommand='node'\ntext=\"\"\"\n[mcp_servers.real]\nenabled=true\n\"\"\"\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                store.SetEnabled(path, ["real"], false, store.Load(path).Hash);
                Equal("[mcp_servers.real]\nenabled = false\n" + source["[mcp_servers.real]\n".Length..], File.ReadAllText(path));
            });
            Test(path =>
            {
                const string source = "[mcp_servers.remote]\nurl='https://example.invalid/api'\nenabled=false\n[mcp_servers.node_repl]\ncommand='node'\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                var snapshot = store.Load(path);
                Assert(snapshot.Servers.Single(x => x.Key == "remote").IsRemote, "Remote missing");
                Assert(snapshot.Servers.Single(x => x.Key == "node_repl").IsProtected, "Helper not protected");
                Refused(() => store.SetEnabled(path, ["remote", "node_repl"], false, snapshot.Hash));
                Equal(source, File.ReadAllText(path));
            });
            Test(path =>
            {
                const string source = "[mcp_servers.example]\nenabled=true\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                var old = store.Load(path);
                File.AppendAllText(path, "# changed\n");
                Refused(() => store.SetEnabled(path, ["example"], false, old.Hash));
                Equal(source + "# changed\n", File.ReadAllText(path));
                Assert(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.bak").Any(), "Stale edit made backup");
            });
            Test(path =>
            {
                File.WriteAllText(path, "[mcp_servers.example]\nenabled=true\n");
                var store = new ConfigStore();
                var change = store.SetEnabled(path, ["example"], false, store.Load(path).Hash);
                File.AppendAllText(path, "# external edit\n");
                var current = File.ReadAllBytes(path);
                Refused(() => store.Undo(change));
                Assert(current.SequenceEqual(File.ReadAllBytes(path)), "Undo overwrote external edit");
                Refused(() => new ConfigStore().Undo(change));
            });
            Test(path =>
            {
                File.WriteAllText(path, "[mcp_servers.example]\nenabled=true\n");
                var store = new ConfigStore();
                var change = store.SetEnabled(path, ["example"], false, store.Load(path).Hash);
                File.AppendAllText(change.BackupPath, "# tampered\n");
                Refused(() => store.Undo(change));
                Assert(!store.Load(path).Servers.Single().Enabled, "Tampered backup restored");
            });
            Test(path =>
            {
                var keys = Enumerable.Range(1, 9).Select(i => "hostinger_" + i).ToArray();
                var source = string.Concat(keys.Select(k => $"[mcp_servers.{k}]\ncommand='node'\nenabled=true # original\n"));
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                var change = store.SetEnabled(path, keys, false, store.Load(path).Hash);
                Assert(change.Snapshot.Servers.Count == 9 && change.Snapshot.Servers.All(s => !s.Enabled), "Batch not disabled");
                Assert(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.bak").Count() == 1, "Batch not atomic");
                Equal(source.Replace("enabled=true", "enabled=false"), File.ReadAllText(path));
                store.Undo(change);
                Equal(source, File.ReadAllText(path));
            });
            Test(path =>
            {
                foreach (var source in new[] {
                    "[mcp_servers.x]\nenabled='fixture-secret'\n",
                    "secret = \"fixture-secret\n",
                    "[mcp_servers.x]\nenabled=true\nenabled=false\n",
                    "[mcp_servers.x]\ncommand='node'\n[mcp_servers.x]\ncommand='other'\n",
                    "[mcp_servers.x]\nargs=[1]\n" })
                {
                    File.WriteAllText(path, source);
                    try { new ConfigStore().Load(path); throw new Exception("Invalid TOML accepted"); }
                    catch (InvalidOperationException ex)
                    {
                        Assert(!ex.ToString().Contains("fixture-secret"), "Secret leaked in diagnostics");
                        Equal(source, File.ReadAllText(path));
                    }
                }
            });
            Test(path =>
            {
                const string source = "mcp_servers = { inline = { command = 'node', enabled = true } }\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                Refused(() => store.SetEnabled(path, ["inline"], false, store.Load(path).Hash));
                Equal(source, File.ReadAllText(path));
            });
            Test(path =>
            {
                File.WriteAllText(path, "[mcp_servers.x]\nenabled=true\n");
                var store = new ConfigStore();
                var hash = store.Load(path).Hash;
                using var collision = new FileStream(path + ".resourcedesk.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                Refused(() => store.SetEnabled(path, ["x"], false, hash));
                Assert(store.Load(path).Servers.Single().Enabled, "Lock collision changed file");
            });
            Test(path =>
            {
                const string source = "[mcp_servers.x]\nenabled=true\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                Refused(() => store.SetEnabled(path, ["x", "missing"], false, store.Load(path).Hash));
                Equal(source, File.ReadAllText(path));
                store.SetEnabled(path, ["x", "x"], false, store.Load(path).Hash);
                Equal(source.Replace("true", "false"), File.ReadAllText(path));
            });
            Test(path =>
            {
                const string source = "# Unicode: ä 日本 😀\n[[unrelated.rows]]\nvalue = [1, 2, 3]\n[mcp_servers.\"quoted\\u002Ename\"]\ncommand='node'\nenabled=true\n";
                File.WriteAllText(path, source);
                var store = new ConfigStore();
                store.SetEnabled(path, ["quoted.name"], false, store.Load(path).Hash);
                Equal(source.Replace("enabled=true", "enabled=false"), File.ReadAllText(path));
            });
            Test(path =>
            {
                File.WriteAllText(path, "[mcp_servers.x]\nenabled=true\n");
                var store = new ConfigStore();
                var snapshot = store.Load(path);
                using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
                    Refused(() => store.SetEnabled(path, ["x"], false, snapshot.Hash));
                Assert(store.Load(path).Servers.Single().Enabled, "Concurrent writer guard changed file");
            });
            return count;
        }
        finally
        {
            // Only this test's freshly generated directory, never any real user configuration.
            Directory.Delete(root, true);
        }
    }
    private static void Equal(string expected, string actual) => Assert(expected == actual, "Fixture text mismatch");
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Refused(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Unsafe fixture operation was accepted");
    }
}
