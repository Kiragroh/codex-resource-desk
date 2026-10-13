using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ResourceDesk.Core;

/// <summary>Explicit user actions only. Each selected process is reauthorized from live evidence.</summary>
public sealed class ProcessController
{
    public StopResult Stop(ComponentInfo component, ConfigSnapshot config)
    {
        var failures = new List<string>();
        int stopped = 0, gone = 0;
        if (!component.CanControl || component.ServerKeys.Count == 0 || component.Key is "core" or "other" or "computer-use" or "node-repl" or "mcp-proxy" or "ambiguous")
            return new(0, 0, ["This component is read-only."]);
        var sampler = new WindowsSampler();
        // Children first: removing a parent first would destroy the proof of ancestry.
        foreach (var expected in component.Processes.OrderByDescending(p => p.StartedUtc).DistinctBy(p => p.Id))
        {
            try
            {
                var freshConfig = new ConfigStore().Load(config.Path);
                var current = sampler.Capture();
                var live = current.Processes.FirstOrDefault(p => p.Id == expected.Id);
                if (live == null) { gone++; continue; }
                var authorized = ProcessAttribution.Build(current, freshConfig).Components.FirstOrDefault(c => c.Key == component.Key);
                if (!CanStopSelection(component, expected, live, authorized))
                { failures.Add($"PID {expected.Id}: identity, ownership or server permissions changed; left running."); continue; }
                using SafeProcessHandle handle = OpenProcess(0x0001 | 0x1000 | 0x00100000, false, expected.Id);
                if (handle.IsInvalid)
                {
                    int code = Marshal.GetLastWin32Error();
                    if (code == 87) { gone++; continue; }
                    throw new Win32Exception(code);
                }
                if (!GetProcessTimes(handle, out long creation, out _, out _, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var path = new StringBuilder(32768);
                int size = path.Capacity;
                if (!QueryFullProcessImageName(handle, 0, path, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var bound = live! with { StartedUtc = DateTime.FromFileTimeUtc(creation), ExecutablePath = path.ToString() };
                if (!ProcessAttribution.SameIdentity(expected, bound))
                { failures.Add($"PID {expected.Id}: process identity changed before stop; left running."); continue; }
                // Terminate only this exact, identity-checked kernel handle. Never recurse by PID.
                if (!TerminateProcess(handle, 1))
                {
                    if (WaitForSingleObject(handle, 0) == 0) { gone++; continue; }
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                if (WaitForSingleObject(handle, 2000) == 0) stopped++;
                else failures.Add($"PID {expected.Id}: stop requested but exit was not confirmed.");
            }
            catch (Exception ex)
            {
                failures.Add($"PID {expected.Id}: stop refused or failed ({ex.GetType().Name}).");
            }
        }
        return new(stopped, gone, failures);
    }

    // Pure policy gate, also used by fixture tests without terminating any process.
    public static bool CanStopSelection(ComponentInfo requested, ProcessInfo expected, ProcessInfo current, ComponentInfo? authorized)
        => authorized != null && requested.CanControl && authorized.CanControl && authorized.ServerKeys.Count > 0 &&
           requested.Key == authorized.Key && requested.ServerKeys.Order().SequenceEqual(authorized.ServerKeys.Order()) &&
           ProcessAttribution.SameIdentity(expected, current) && authorized.Processes.Any(p => ProcessAttribution.SameIdentity(expected, p));

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
}
