namespace WindowRPC.Models;

internal sealed class DefaultSettings
{
    public string Details { get; init; } = "Currently using:";

    public string State { get; init; } = "timestamp - appname";

    public int Interval { get; init; } = 5;
}
