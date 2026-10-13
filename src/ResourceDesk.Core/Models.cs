namespace ResourceDesk.Core;

public sealed record ServerConfig(string Key, string DisplayName, bool Enabled, string Command, string[] Args, bool IsRemote, bool IsProtected = false);
public sealed record ConfigSnapshot(string Path, string Hash, IReadOnlyList<ServerConfig> Servers, IReadOnlyList<string> Warnings);
public sealed record ConfigChange(ConfigSnapshot Snapshot, string BackupPath);
public sealed record ProcessInfo(int Id, int ParentId, DateTime StartedUtc, string Name, string CommandLine, string ExecutablePath, long WorkingSetBytes, long PrivateBytes, long PrivateWorkingSetBytes, long CpuTicks, int SessionId);
public sealed record SystemMemory(long TotalBytes, long AvailableBytes, long CommitBytes, long CommitLimitBytes, long KernelPagedBytes, long KernelNonPagedBytes);
public sealed record ProcessSnapshot(DateTime CapturedUtc, SystemMemory Memory, IReadOnlyList<ProcessInfo> Processes, double ScanMilliseconds, IReadOnlyList<string> Warnings);
public sealed record ComponentInfo(string Key, string Name, string Subtitle, string Color, IReadOnlyList<string> ServerKeys, bool? Enabled, bool CanControl, IReadOnlyList<ProcessInfo> Processes, string Explanation)
{
    public int Count => Processes.Count;
    public long RamBytes => Processes.Sum(p => p.PrivateWorkingSetBytes);
    public long CommitBytes => Processes.Sum(p => p.PrivateBytes);
}
public sealed record ResourceSnapshot(ProcessSnapshot Raw, IReadOnlyList<ComponentInfo> Components, IReadOnlyList<ProcessInfo> CodexProcesses)
{
    public long CodexRamBytes => CodexProcesses.Sum(p => p.PrivateWorkingSetBytes);
    public long CodexCommitBytes => CodexProcesses.Sum(p => p.PrivateBytes);
}
public sealed record StopResult(int Stopped, int AlreadyExited, IReadOnlyList<string> Failures);
