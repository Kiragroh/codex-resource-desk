using System.Diagnostics;
using System.Text.Json;
using ResourceDesk.Core;
using ResourceDesk.Tests;

if (args.Contains("--sample"))
{
    var config = new ConfigStore().Load(ConfigStore.DefaultPath);
    var sampler = new WindowsSampler();
    for (int i = 0; i < 2; i++)
    {
        var raw = sampler.Capture(); var snapshot = ProcessAttribution.Build(raw, config);
        Console.WriteLine(JsonSerializer.Serialize(new { raw.ScanMilliseconds, system = raw.Memory, processes = raw.Processes.Count, codex = snapshot.CodexProcesses.Count, privateRam = snapshot.CodexRamBytes, warnings = raw.Warnings, components = snapshot.Components.Select(c => new { c.Key, c.Name, c.Enabled, c.CanControl, c.Count, c.RamBytes, c.CommitBytes }) }));
        var me = raw.Processes.FirstOrDefault(p => p.Id == Environment.ProcessId);
        if (me != null) { using var p = Process.GetCurrentProcess(); if (me.PrivateWorkingSetBytes > me.WorkingSetBytes || Math.Abs(me.PrivateBytes - p.PrivateMemorySize64) > 64 * 1048576) throw new Exception("Native memory sanity check failed"); }
    }
    return;
}
var count = ConfigTests.Run() + ProcessTests.Run();
Console.WriteLine($"PASS: {count} safety fixture groups; no live integrations modified.");
