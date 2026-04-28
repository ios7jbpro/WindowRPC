namespace WindowRPC.Models;

internal sealed class OverrideEditorItem
{
    public string Name { get; set; } = string.Empty;

    public string Logo { get; set; } = "rpc_icon";

    public string Details { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string MatchMode { get; set; } = "inline";

    public string OverrideMode { get; set; } = string.Empty;

    public bool Ignore { get; set; }

    public string Player { get; set; } = string.Empty;

    public string Artwork { get; set; } = string.Empty;

    public string ArtworkSources { get; set; } = string.Empty;

    public bool MProgress { get; set; }

    public OverrideEntry ToOverrideEntry()
    {
        var normalizedOverrideMode = NormalizeMode(OverrideMode);
        var normalizedMatchMode = NormalizeMode(MatchMode);
        var artworkSources = ArtworkSources
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new OverrideEntry
        {
            Logo = string.IsNullOrWhiteSpace(Logo) ? "rpc_icon" : Logo.Trim(),
            Details = Details,
            State = State,
            MatchMode = string.IsNullOrWhiteSpace(normalizedMatchMode) ? null : normalizedMatchMode,
            OverrideMode = string.IsNullOrWhiteSpace(normalizedOverrideMode) ? null : normalizedOverrideMode,
            Ignore = Ignore,
            Player = string.IsNullOrWhiteSpace(Player) ? null : Player.Trim(),
            Artwork = string.IsNullOrWhiteSpace(Artwork) ? null : Artwork.Trim(),
            ArtworkSources = artworkSources.Length == 0 ? null : artworkSources,
            MProgress = MProgress
        };
    }

    public static OverrideEditorItem FromRule(OverrideRule rule)
    {
        return new OverrideEditorItem
        {
            Name = rule.Name,
            Logo = rule.Entry.Logo ?? "rpc_icon",
            Details = rule.Entry.Details ?? string.Empty,
            State = rule.Entry.State ?? string.Empty,
            MatchMode = rule.Entry.MatchMode ?? "inline",
            OverrideMode = rule.Entry.OverrideMode ?? string.Empty,
            Ignore = rule.Entry.Ignore,
            Player = rule.Entry.Player ?? string.Empty,
            Artwork = rule.Entry.Artwork ?? string.Empty,
            ArtworkSources = rule.Entry.ArtworkSources is { Length: > 0 }
                ? string.Join(", ", rule.Entry.ArtworkSources)
                : string.Empty,
            MProgress = rule.Entry.MProgress
        };
    }

    private static string NormalizeMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "normal" => string.Empty,
            _ => value.Trim().ToLowerInvariant()
        };
    }
}
