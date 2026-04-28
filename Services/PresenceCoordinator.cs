using System.Diagnostics;
using WindowRPC.Models;

namespace WindowRPC.Services;

internal sealed class PresenceCoordinator : IDisposable
{
    private const string DiscordClientId = "1275126036262031452";
    private const string LegacyGenericMediaOverrideName = "DeleteThisOverrideToStopMediaRPC";

    private readonly ConfigurationService _configurationService;
    private readonly ForegroundWindowWatcher _windowWatcher;
    private readonly MediaSessionWatcher _mediaSessionWatcher;
    private readonly DiscordRpcClient _discordRpcClient;
    private readonly ArtworkService _artworkService;
    private readonly Stopwatch _sessionStopwatch = Stopwatch.StartNew();
    private readonly Dictionary<string, DateTimeOffset> _overrideStartTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Timers.Timer _fallbackTimer;
    private readonly object _sync = new();

    private WindowSnapshot _currentWindow = new();
    private MediaSnapshot _currentMedia = MediaSnapshot.Empty;
    private ResolvedPresence? _lastPresence;

    public PresenceCoordinator(
        ConfigurationService configurationService,
        ForegroundWindowWatcher windowWatcher,
        MediaSessionWatcher mediaSessionWatcher)
    {
        _configurationService = configurationService;
        _windowWatcher = windowWatcher;
        _mediaSessionWatcher = mediaSessionWatcher;
        _discordRpcClient = new DiscordRpcClient(DiscordClientId);
        _artworkService = new ArtworkService();
        _fallbackTimer = new System.Timers.Timer(5000);
        _fallbackTimer.Elapsed += (_, _) => Refresh();
    }

    public void Start()
    {
        _configurationService.ConfigurationChanged += OnConfigurationChanged;
        _windowWatcher.ForegroundChanged += OnForegroundChanged;
        _mediaSessionWatcher.MediaChanged += OnMediaChanged;
        _windowWatcher.Start();
        _fallbackTimer.Start();
        Refresh();
    }

    public void Dispose()
    {
        _fallbackTimer.Stop();
        _fallbackTimer.Dispose();
        try
        {
            _discordRpcClient.ClearPresenceAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore shutdown cleanup failures.
        }

        _discordRpcClient.Dispose();
        _artworkService.Dispose();
        _configurationService.ConfigurationChanged -= OnConfigurationChanged;
        _windowWatcher.ForegroundChanged -= OnForegroundChanged;
        _mediaSessionWatcher.MediaChanged -= OnMediaChanged;
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) => Refresh();

    private void OnForegroundChanged(object? sender, WindowSnapshot snapshot)
    {
        _currentWindow = snapshot;
        Refresh();
    }

    private void OnMediaChanged(object? sender, MediaSnapshot snapshot)
    {
        _currentMedia = snapshot;
        Refresh();
    }

    private void Refresh()
    {
        lock (_sync)
        {
            var presence = ResolvePresence();
            if (presence.Equals(_lastPresence))
            {
                return;
            }

            _lastPresence = presence;
            PublishPresence(presence);
        }
    }

    private ResolvedPresence ResolvePresence()
    {
        var config = _configurationService.Current;
        var activeWindow = _currentWindow.Title;
        var visibleTitles = _windowWatcher.GetVisibleWindowTitles();

        var gameRule = config.Overrides
            .Where(rule => string.Equals(rule.Entry.OverrideMode, "game", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(rule => WindowMatchesAny(rule, visibleTitles));

        if (gameRule is not null)
        {
            return BuildPresence(gameRule.Name, gameRule.Entry, activeWindow, _currentMedia, hidden: gameRule.Entry.Ignore);
        }

        if (_currentMedia.IsOverrideEligible)
        {
            var mediaRule = config.Overrides
                .Where(rule => string.Equals(rule.Entry.OverrideMode, "media", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(rule => MediaMatches(rule, _currentMedia));

            if (mediaRule is not null)
            {
                return BuildPresence(mediaRule.Name, mediaRule.Entry, activeWindow, _currentMedia, hidden: mediaRule.Entry.Ignore, useArtwork: true);
            }
        }

        var normalRule = config.Overrides.FirstOrDefault(rule => WindowMatches(rule.Name, activeWindow, rule.Entry.MatchMode));
        if (normalRule is not null)
        {
            return BuildPresence(normalRule.Name, normalRule.Entry, activeWindow, _currentMedia, hidden: normalRule.Entry.Ignore);
        }

        var elapsed = FormatElapsed(_sessionStopwatch.Elapsed);
        return new ResolvedPresence
        {
            State = Truncate(RenderTemplate(config.Default.State, activeWindow, elapsed, elapsed, _currentMedia)),
            Details = Truncate(RenderTemplate(config.Default.Details, activeWindow, elapsed, elapsed, _currentMedia)),
            Logo = "rpc_icon",
            Hidden = false
        };
    }

    private ResolvedPresence BuildPresence(
        string ruleName,
        OverrideEntry entry,
        string activeWindow,
        MediaSnapshot media,
        bool hidden,
        bool useArtwork = false)
    {
        if (hidden)
        {
            return new ResolvedPresence
            {
                Hidden = true
            };
        }

        if (!_overrideStartTimes.TryGetValue(ruleName, out var startedAt))
        {
            startedAt = DateTimeOffset.Now;
            _overrideStartTimes[ruleName] = startedAt;
        }

        var totalElapsed = FormatElapsed(_sessionStopwatch.Elapsed);
        var overrideElapsed = FormatElapsed(DateTimeOffset.Now - startedAt);
        var logo = string.IsNullOrWhiteSpace(entry.Logo) ? "rpc_icon" : entry.Logo!.Trim();
        var startTimestampUnix = default(long?);
        var endTimestampUnix = default(long?);
        if (useArtwork)
        {
            try
            {
                var artworkUrl = _artworkService.ResolveArtworkUrlAsync(entry, media).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(artworkUrl))
                {
                    logo = artworkUrl;
                }
            }
            catch
            {
                // Keep the configured logo if artwork providers fail.
            }
        }

        if (entry.MProgress && media.IsPlaying && media.TotalSeconds > 0)
        {
            var now = DateTimeOffset.UtcNow;
            var clampedPosition = Math.Clamp(media.PositionSeconds, 0, media.TotalSeconds);
            startTimestampUnix = now.AddSeconds(-clampedPosition).ToUnixTimeSeconds();
            endTimestampUnix = now.AddSeconds(media.TotalSeconds - clampedPosition).ToUnixTimeSeconds();
        }

        return new ResolvedPresence
        {
            State = Truncate(RenderTemplate(entry.State, activeWindow, totalElapsed, overrideElapsed, media)),
            Details = Truncate(RenderTemplate(entry.Details, activeWindow, totalElapsed, overrideElapsed, media)),
            Logo = logo,
            Hidden = false,
            StartTimestampUnix = startTimestampUnix,
            EndTimestampUnix = endTimestampUnix
        };
    }

    private static string RenderTemplate(
        string? template,
        string activeWindow,
        string totalElapsed,
        string overrideElapsed,
        MediaSnapshot media)
    {
        return (template ?? string.Empty)
            .Replace("appname", activeWindow, StringComparison.Ordinal)
            .Replace("totaltimestamp", totalElapsed, StringComparison.Ordinal)
            .Replace("timestamp", overrideElapsed, StringComparison.Ordinal)
            .Replace("mtitle", media.Title, StringComparison.Ordinal)
            .Replace("martist", media.Artist, StringComparison.Ordinal)
            .Replace("malbum", media.Album, StringComparison.Ordinal)
            .Replace("mtotal", media.Total, StringComparison.Ordinal)
            .Replace("mcollapsed", media.Position, StringComparison.Ordinal)
            .Replace("mplayer", media.Player, StringComparison.Ordinal);
    }

    private static bool WindowMatchesAny(OverrideRule rule, IReadOnlyList<string> titles)
    {
        return titles.Any(title => WindowMatches(rule.Name, title, rule.Entry.MatchMode));
    }

    private static bool MediaMatches(OverrideRule rule, MediaSnapshot media)
    {
        var configuredPlayerFilter = !string.IsNullOrWhiteSpace(rule.Entry.Player)
            ? rule.Entry.Player.Trim()
            : string.Equals(rule.Name, LegacyGenericMediaOverrideName, StringComparison.OrdinalIgnoreCase)
                ? null
                : rule.Name.Trim();

        if (string.IsNullOrWhiteSpace(configuredPlayerFilter))
        {
            return true;
        }

        if (!PlayerMatches(configuredPlayerFilter, media.Player))
        {
            return false;
        }

        return true;
    }

    private static bool PlayerMatches(string configuredFilter, string detectedPlayer)
    {
        if (string.IsNullOrWhiteSpace(detectedPlayer))
        {
            return false;
        }

        var aliases = GetPlayerAliases(detectedPlayer);
        return aliases.Any(alias => alias.Contains(configuredFilter, StringComparison.OrdinalIgnoreCase))
            || detectedPlayer.Contains(configuredFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> GetPlayerAliases(string detectedPlayer)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddAlias(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                aliases.Add(value.Trim());
            }
        }

        AddAlias(detectedPlayer);

        foreach (var segment in detectedPlayer.Split(['!', '\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddAlias(segment);

            if (segment.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                AddAlias(segment[..^4]);
            }

            var underscoreIndex = segment.IndexOf('_');
            if (underscoreIndex > 0)
            {
                AddAlias(segment[..underscoreIndex]);
            }

            foreach (var dotSegment in segment.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddAlias(dotSegment);
            }
        }

        return aliases;
    }

    private static bool WindowMatches(string ruleName, string title, string? matchMode)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return string.Equals(matchMode, "exact", StringComparison.OrdinalIgnoreCase)
            ? string.Equals(title, ruleName, StringComparison.OrdinalIgnoreCase)
            : title.Contains(ruleName, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatElapsed(TimeSpan elapsed) => $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";

    private static string Truncate(string text, int maxLength = 60)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..(maxLength - 3)] + "...";
    }

    private void PublishPresence(ResolvedPresence presence)
    {
        if (presence.Hidden)
        {
            Debug.WriteLine("Presence hidden due to ignore override.");
            _ = _discordRpcClient.ClearPresenceAsync();
            return;
        }

        Debug.WriteLine(
            $"Presence update -> State: '{presence.State}', Details: '{presence.Details}', Logo: '{presence.Logo}', Start: '{presence.StartTimestampUnix}', End: '{presence.EndTimestampUnix}'");

        _ = _discordRpcClient.SetPresenceAsync(presence);
    }
}
