namespace WindowRPC.Models;

internal sealed class WindowSnapshot
{
    public string Title { get; init; } = "No active window";

    public string? ProcessName { get; init; }

    public uint? ProcessId { get; init; }
}
