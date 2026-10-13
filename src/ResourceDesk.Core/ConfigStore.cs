using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace ResourceDesk.Core;

/// <summary>Lossless edits of explicit global MCP sections. Configuration contents never enter diagnostics.</summary>
public sealed class ConfigStore
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly Dictionary<string, UndoRecord> undo = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private sealed record UndoRecord(string Path, string BeforeHash, string AfterHash);
    private sealed record Parsed(byte[] Bytes, string Text, bool Bom, DocumentSyntax Document, TomlTable Model);
    private sealed record Edit(int Offset, int Length, string Value);

    public static string DefaultPath => System.IO.Path.Combine(
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_HOME"))
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
            : Environment.GetEnvironmentVariable("CODEX_HOME")!, "config.toml");

    public ConfigSnapshot Load(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        return Snapshot(path, Parse(File.ReadAllBytes(path)));
    }

    public ConfigChange SetEnabled(string path, IReadOnlyList<string> keys, bool enabled, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(keys);
        path = System.IO.Path.GetFullPath(path);
        lock (gate)
        {
            using var transaction = AcquireTransaction(path);
            using var original = OpenSource(path);
            var before = Parse(ReadAll(original));
            RequireHash(before.Bytes, expectedHash);
            var requested = keys.Distinct(StringComparer.Ordinal).ToArray();
            if (requested.Length == 0) throw new InvalidOperationException("Select at least one integration.");
            var servers = Servers(before.Model);
            var edits = new List<Edit>();
            foreach (var key in requested)
            {
                if (Protected(key)) throw new InvalidOperationException("This integration is a protected core helper.");
                if (!servers.TryGetValue(key, out var value) || value is not TomlTable server)
                    throw new InvalidOperationException("A selected integration is no longer present.");
                var sections = before.Document.Tables.OfType<TableSyntax>()
                    .Where(t => Parts(t.Name!).SequenceEqual(new[] { "mcp_servers", key }, StringComparer.Ordinal)).ToArray();
                if (sections.Length != 1)
                    throw new InvalidOperationException("This integration uses a configuration layout that cannot be edited safely. Use an explicit MCP section.");
                var section = sections[0];
                var flags = section.Items.Where(i => Parts(i.Key!).SequenceEqual(new[] { "enabled" }, StringComparer.Ordinal)).ToArray();
                if (flags.Length > 1 || (server.ContainsKey("enabled") && flags.Length != 1))
                    throw new InvalidOperationException("The enabled setting cannot be located unambiguously.");
                if (flags.Length == 1)
                {
                    if (server["enabled"] is not bool) throw new InvalidOperationException("An enabled setting must be a boolean.");
                    var span = flags[0].Value!.Span;
                    var token = before.Text.Substring(span.Offset, span.Length);
                    if (token != "true" && token != "false") throw new InvalidOperationException("The enabled setting cannot be edited safely.");
                    edits.Add(new(span.Offset, span.Length, enabled ? "true" : "false"));
                }
                else
                {
                    // Insert after the real AST header's complete line, preserving any comment.
                    var end = before.Text.IndexOf('\n', section.CloseBracket!.Span.Offset);
                    var newline = before.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                    edits.Add(end < 0
                        ? new(before.Text.Length, 0, newline + "enabled = " + (enabled ? "true" : "false") + newline)
                        : new(end + 1, 0, "enabled = " + (enabled ? "true" : "false") + newline));
                }
                server["enabled"] = enabled;
            }
            var text = before.Text;
            foreach (var edit in edits.OrderByDescending(e => e.Offset))
                text = text.Remove(edit.Offset, edit.Length).Insert(edit.Offset, edit.Value);
            var bytes = Encode(text, before.Bom);
            var after = Parse(bytes);
            if (!Equivalent(before.Model, after.Model))
                throw new InvalidOperationException("The edit would change unrelated settings and was refused.");
            var beforeHash = Hash(before.Bytes);
            var afterHash = Hash(bytes);
            var backup = Commit(path, bytes, beforeHash);
            undo[backup] = new(path, beforeHash, afterHash);
            return new(Snapshot(path, after), backup);
        }
    }

    public ConfigSnapshot Undo(ConfigChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (gate)
        {
            if (!undo.TryGetValue(change.BackupPath, out var record) ||
                !string.Equals(record.Path, change.Snapshot.Path, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(record.AfterHash, change.Snapshot.Hash, StringComparison.Ordinal))
                throw new InvalidOperationException("This change has no undo record in this application session.");
            using var transaction = AcquireTransaction(record.Path);
            using var original = OpenSource(record.Path);
            RequireHash(ReadAll(original), record.AfterHash);
            var bytes = File.ReadAllBytes(change.BackupPath);
            RequireHash(bytes, record.BeforeHash);
            var restored = Parse(bytes);
            _ = Commit(record.Path, bytes, record.AfterHash);
            undo.Remove(change.BackupPath);
            return Snapshot(record.Path, restored);
        }
    }

    private static FileStream AcquireTransaction(string path)
    {
        try
        {
            // Persistent empty sidecar, shared by all Resource Desk instances. Never contains settings.
            return new FileStream(path + ".resourcedesk.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { throw new InvalidOperationException("The configuration is busy. Refresh and try again."); }
    }

    private static FileStream OpenSource(string path)
    {
        try { return new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); }
        catch (IOException) { throw new InvalidOperationException("The configuration is busy or unavailable. Refresh and try again."); }
    }

    private static byte[] ReadAll(FileStream file)
    {
        using var memory = new MemoryStream();
        file.CopyTo(memory);
        return memory.ToArray();
    }

    private static string Commit(string path, byte[] bytes, string expectedHash)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var temporary = path + ".resourcedesk." + suffix + ".tmp";
        var backup = path + ".resourcedesk." + suffix + ".bak";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(true);
            }
            RequireHash(File.ReadAllBytes(path), expectedHash);
            File.Replace(temporary, path, backup, false);
            // An external atomic writer can rename despite the source read lock. The actual
            // replaced version is retained atomically. Never attempt a second replacement:
            // that recovery could itself race another external writer.
            if (Hash(File.ReadAllBytes(backup)) != expectedHash)
            {
                throw new ConfigurationConflictException();
            }
            RequireHash(File.ReadAllBytes(path), Hash(bytes));
            return backup;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static Parsed Parse(byte[] bytes)
    {
        try
        {
            var bom = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf });
            var text = Utf8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var document = Toml.Parse(text);
            if (document.HasErrors) throw new FormatException();
            var model = Toml.ToModel(document);
            _ = Servers(model);
            return new(bytes, text, bom, document, model);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            // Tomlyn diagnostics may quote a credential-bearing line. Deliberately discard them.
            throw new InvalidOperationException("The configuration is not valid supported UTF-8 TOML. No changes were made.");
        }
    }

    private static TomlTable Servers(TomlTable model)
    {
        if (!model.TryGetValue("mcp_servers", out var value)) return new TomlTable();
        if (value is not TomlTable servers) throw new FormatException();
        foreach (var pair in servers)
        {
            if (pair.Value is not TomlTable server) throw new FormatException();
            if (server.TryGetValue("enabled", out var flag) && flag is not bool) throw new FormatException();
            if (server.TryGetValue("command", out var command) && command is not string) throw new FormatException();
            if (server.TryGetValue("url", out var url) && url is not string) throw new FormatException();
            if (server.TryGetValue("args", out var args) && (args is not TomlArray array || array.Any(a => a is not string))) throw new FormatException();
        }
        return servers;
    }

    private static ConfigSnapshot Snapshot(string path, Parsed parsed)
    {
        var result = new List<ServerConfig>();
        foreach (var pair in Servers(parsed.Model))
        {
            var table = (TomlTable)pair.Value;
            result.Add(new(pair.Key, pair.Key, !table.TryGetValue("enabled", out var flag) || (bool)flag,
                table.TryGetValue("command", out var command) ? (string)command : "",
                table.TryGetValue("args", out var args) ? ((TomlArray)args).Cast<string>().ToArray() : [],
                table.ContainsKey("url"), Protected(pair.Key)));
        }
        return new(path, Hash(parsed.Bytes), result, []);
    }

    private static bool Protected(string key) => string.Equals(key, "node_repl", StringComparison.OrdinalIgnoreCase);
    private static string[] Parts(KeySyntax key) => new[] { KeyPart(key.Key!) }.Concat(key.DotKeys.Select(k => KeyPart(k.Key!))).ToArray();
    private static string KeyPart(BareKeyOrStringValueSyntax key) => key switch
    {
        BareKeySyntax bare => bare.Key!.Text!,
        StringValueSyntax quoted => quoted.Value!,
        _ => throw new InvalidOperationException("Unsupported configuration key syntax.")
    };
    private static byte[] Encode(string text, bool bom) => bom ? new byte[] { 0xef, 0xbb, 0xbf }.Concat(Utf8.GetBytes(text)).ToArray() : Utf8.GetBytes(text);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void RequireHash(byte[] bytes, string expected)
    {
        if (!string.Equals(Hash(bytes), expected, StringComparison.Ordinal))
            throw new InvalidOperationException("The configuration or backup changed. Refresh before editing; no stale version will be applied.");
    }
    private static bool Equivalent(object? a, object? b)
    {
        if (a is TomlTable left && b is TomlTable right)
            return left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var v) && Equivalent(p.Value, v));
        if (a is IEnumerable la && b is IEnumerable lb && a is not string && b is not string)
        {
            var leftItems = la.Cast<object?>().ToArray();
            var rightItems = lb.Cast<object?>().ToArray();
            return leftItems.Length == rightItems.Length && Enumerable.Range(0, leftItems.Length).All(i => Equivalent(leftItems[i], rightItems[i]));
        }
        return Equals(a, b);
    }
}

public sealed class ConfigurationConflictException : InvalidOperationException
{
    public ConfigurationConflictException() : base("An external configuration edit raced replacement. The displaced version is preserved in the adjacent .resourcedesk backup; reconcile it before editing again.") { }
}
