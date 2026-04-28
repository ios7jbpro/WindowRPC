namespace WindowRPC.Models;

internal sealed class ResolvedPresence : IEquatable<ResolvedPresence>
{
    public string State { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public string Logo { get; init; } = "rpc_icon";

    public bool Hidden { get; init; }

    public long? StartTimestampUnix { get; init; }

    public long? EndTimestampUnix { get; init; }

    public bool Equals(ResolvedPresence? other)
    {
        if (other is null)
        {
            return false;
        }

        return State == other.State
            && Details == other.Details
            && Logo == other.Logo
            && Hidden == other.Hidden
            && StartTimestampUnix == other.StartTimestampUnix
            && EndTimestampUnix == other.EndTimestampUnix;
    }

    public override bool Equals(object? obj) => Equals(obj as ResolvedPresence);

    public override int GetHashCode() => HashCode.Combine(State, Details, Logo, Hidden, StartTimestampUnix, EndTimestampUnix);
}
