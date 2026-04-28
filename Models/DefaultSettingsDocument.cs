using System.Text.Json.Serialization;

namespace WindowRPC.Models;

internal sealed class DefaultSettingsDocument
{
    [JsonPropertyName("default")]
    public DefaultSettings Default { get; init; } = new();
}
