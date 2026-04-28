namespace WindowRPC.Models;

internal sealed class OverrideRule
{
    public required string Name { get; init; }

    public required OverrideEntry Entry { get; init; }
}
