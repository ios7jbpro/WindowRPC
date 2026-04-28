using WindowRPC.Models;

namespace WindowRPC.Services;

internal sealed class RuntimeConfiguration
{
    public RuntimeConfiguration(DefaultSettings defaultSettings, IReadOnlyList<OverrideRule> overrides)
    {
        Default = defaultSettings;
        Overrides = overrides;
    }

    public DefaultSettings Default { get; }

    public IReadOnlyList<OverrideRule> Overrides { get; }
}
