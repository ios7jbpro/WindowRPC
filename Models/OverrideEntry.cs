using System.Text.Json.Serialization;

namespace WindowRPC.Models;

internal sealed class OverrideEntry
{
    [JsonPropertyName("logo")]
    public string? Logo { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("match_mode")]
    public string? MatchMode { get; init; }

    [JsonPropertyName("override_mode")]
    public string? OverrideMode { get; init; }

    [JsonPropertyName("ignore")]
    public bool Ignore { get; init; }

    [JsonPropertyName("player")]
    public string? Player { get; init; }

    [JsonPropertyName("artwork")]
    public string? Artwork { get; init; }

    [JsonPropertyName("artwork_sources")]
    public string[]? ArtworkSources { get; init; }

    [JsonPropertyName("mprogress")]
    public bool MProgress { get; init; }
}
