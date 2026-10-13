using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace ResourceDesk.Core;

/// <summary>CPU-only, native private resident memory sampling. Commands remain internal.</summary>
public sealed class WindowsSampler
{
    private readonly object gate = new();
    private readonly Dictionary<(int, long), Identity> identities = new();
    private sealed record Identity(string Path, string Command);

    public ProcessSnapshot Capture()
    {
        lock (gate)
        {
            var timer = Stopwatch.StartNew();
            var warnings = new List<string>();
            var captured = DateTime.UtcNow;
            var memory = ReadMemory(); // Failure must be visible; never show a fabricated all-free system.
            var native = ReadProcesses();
            var live = native.Select(p => (p.Id, p.StartedUtc.Ticks)).ToHashSet();
            foreach (var key in identities.Keys.Where(k => !live.Contains(k)).ToArray()) identities.Remove(key);
            var missing = native.Where(p => p.Id > 0 && !identities.ContainsKey((p.Id, p.StartedUtc.Ticks))).ToArray();
            if (missing.Length > 0)
            {
                try
                {
                    // Initial inventory is one query; subsequent scans query only new identities.
                    string filter = identities.Count == 0 ? "" : " WHERE " + string.Join(" OR ", missing.Select(p => $"ProcessId={p.Id}"));
                    using var searcher = new ManagementObjectSearcher("SELECT ProcessId,CreationDate,ExecutablePath,CommandLine FROM Win32_Process" + filter);
                    searcher.Options.Timeout = TimeSpan.FromSeconds(8);
                    using var rows = searcher.Get();
                    var wanted = missing.ToDictionary(p => p.Id);
                    foreach (ManagementObject row in rows)
                    {
                        using (row)
                        {
                            int pid = Convert.ToInt32(row["ProcessId"]);
                            if (!wanted.TryGetValue(pid, out var p) || row["CreationDate"] is not string date) continue;
                            var started = ManagementDateTimeConverter.ToDateTime(date).ToUniversalTime();
                            // WMI timestamps have microsecond precision. Reject PID reuse during inventory.
                            if (Math.Abs((started - p.StartedUtc).Ticks) > 20) continue;
                            identities[(pid, p.StartedUtc.Ticks)] = new Identity(row["ExecutablePath"] as string ?? "", row["CommandLine"] as string ?? "");
                        }
                    }
                }
                catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException or TimeoutException)
                { warnings.Add("Process identity inventory is incomplete (" + ex.GetType().Name + "). Attribution may undercount."); }
            }
            var result = native.Select(p => identities.TryGetValue((p.Id, p.StartedUtc.Ticks), out var id)
                ? p with { ExecutablePath = id.Path, CommandLine = id.Command } : p).ToArray();
            if (result.Any(p => (p.Name.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrEmpty(p.ExecutablePath)))
                warnings.Add("A possible Codex process has no readable executable identity. Codex attribution may undercount.");
            timer.Stop();
            return new ProcessSnapshot(captured, memory, result, timer.Elapsed.TotalMilliseconds, warnings);
        }
    }

    private static SystemMemory ReadMemory()
    {
        var info = new PerformanceInformation { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (!GetPerformanceInfo(ref info, info.Size)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "System memory sampling failed.");
        long Bytes(nuint pages) => checked((long)pages * (long)info.PageSize);
        return new(Bytes(info.PhysicalTotal), Bytes(info.PhysicalAvailable), Bytes(info.CommitTotal), Bytes(info.CommitLimit), Bytes(info.KernelPaged), Bytes(info.KernelNonpaged));
    }

    private static IReadOnlyList<ProcessInfo> ReadProcesses()
    {
        if (IntPtr.Size != 8) throw new PlatformNotSupportedException("Private resident memory sampling requires Windows x64.");
        int size = 1024 * 1024;
        while (true)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = NtQuerySystemInformation(5, buffer, size, out int needed);
                if (status == unchecked((int)0xC0000004)) { size = Math.Max(checked(size * 2), checked(needed + 65536)); continue; }
                if (status < 0) throw new InvalidOperationException($"Native private resident sampling failed (NTSTATUS 0x{status:X8}).");
                var list = new List<ProcessInfo>();
                int offset = 0;
                do
                {
                    if (offset > size - 208) throw new InvalidOperationException("Native process inventory was truncated.");
                    IntPtr p = IntPtr.Add(buffer, offset);
                    int next = Marshal.ReadInt32(p, 0);
                    long pid = Marshal.ReadInt64(p, 80), parent = Marshal.ReadInt64(p, 88);
                    long created = Marshal.ReadInt64(p, 32);
                    if (pid > 0 && pid <= int.MaxValue && created > 0)
                    {
                        ushort nameLength = unchecked((ushort)Marshal.ReadInt16(p, 56));
                        IntPtr namePointer = Marshal.ReadIntPtr(p, 64);
                        string name = namePointer == IntPtr.Zero ? "" : Marshal.PtrToStringUni(namePointer, nameLength / 2) ?? "";
                        // SYSTEM_PROCESS_INFORMATION x64: WorkingSetPrivateSize=8, WorkingSetSize=144,
                        // PrivatePageCount=200. Shared WorkingSetSize is never used for exclusive RAM.
                        list.Add(new((int)pid, checked((int)parent), DateTime.FromFileTimeUtc(created), name, "", "",
                            Math.Max(0, Marshal.ReadInt64(p, 144)), Math.Max(0, Marshal.ReadInt64(p, 200)), Math.Max(0, Marshal.ReadInt64(p, 8)),
                            Marshal.ReadInt64(p, 40) + Marshal.ReadInt64(p, 48), Marshal.ReadInt32(p, 100)));
                    }
                    if (next == 0) break;
                    if (next < 208 || next > size - offset) throw new InvalidOperationException("Invalid native process inventory layout.");
                    offset += next;
                } while (true);
                return list;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public nuint CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [DllImport("psapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(ref PerformanceInformation info, uint size);
    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int returnLength);
}
