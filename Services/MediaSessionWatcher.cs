using System.Diagnostics;
using WindowRPC.Models;
using Windows.Media.Control;

namespace WindowRPC.Services;

internal sealed class MediaSessionWatcher : IDisposable
{
    private readonly object _sync = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaSnapshot _current = MediaSnapshot.Empty;
    private bool _disposed;

    public event EventHandler<MediaSnapshot>? MediaChanged;

    public MediaSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Start()
    {
        _ = InitializeAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeFromSession();

        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager = null;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            _manager.SessionsChanged += OnSessionsChanged;
            await RefreshSessionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Media session initialization failed: {ex}");
        }
    }

    private async void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        await RefreshSessionAsync().ConfigureAwait(false);
    }

    private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        await RefreshSessionAsync().ConfigureAwait(false);
    }

    private async Task RefreshSessionAsync()
    {
        if (_manager is null || _disposed)
        {
            return;
        }

        var session = _manager.GetCurrentSession();

        lock (_sync)
        {
            if (ReferenceEquals(_session, session))
            {
                return;
            }
        }

        UnsubscribeFromSession();

        lock (_sync)
        {
            _session = session;
        }

        if (session is not null)
        {
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }

        await PublishSnapshotAsync().ConfigureAwait(false);
    }

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        await PublishSnapshotAsync().ConfigureAwait(false);
    }

    private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        await PublishSnapshotAsync().ConfigureAwait(false);
    }

    private async void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        await PublishSnapshotAsync().ConfigureAwait(false);
    }

    private async Task PublishSnapshotAsync()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sync)
        {
            session = _session;
        }

        var snapshot = await CreateSnapshotAsync(session).ConfigureAwait(false);

        lock (_sync)
        {
            if (snapshot.Equals(_current))
            {
                return;
            }

            _current = snapshot;
        }

        MediaChanged?.Invoke(this, snapshot);
    }

    private static async Task<MediaSnapshot> CreateSnapshotAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null)
        {
            return MediaSnapshot.Empty;
        }

        try
        {
            var playbackInfo = session.GetPlaybackInfo();
            var status = playbackInfo.PlaybackStatus;

            var isPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var isPaused = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;

            if (!isPlaying && !isPaused)
            {
                return MediaSnapshot.Empty;
            }

            var mediaProperties = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();

            var snapshot = new MediaSnapshot
            {
                Title = string.IsNullOrWhiteSpace(mediaProperties?.Title) ? "No media playing" : mediaProperties.Title,
                Artist = string.IsNullOrWhiteSpace(mediaProperties?.Artist) ? "Unknown artist" : mediaProperties.Artist,
                Album = string.IsNullOrWhiteSpace(mediaProperties?.AlbumTitle) ? "Unknown album" : mediaProperties.AlbumTitle,
                Total = FormatTime(timeline.EndTime),
                Position = isPaused ? "Paused" : FormatTime(timeline.Position),
                Player = session.SourceAppUserModelId ?? string.Empty,
                TotalSeconds = timeline.EndTime > TimeSpan.Zero ? (int)timeline.EndTime.TotalSeconds : 0,
                PositionSeconds = timeline.Position > TimeSpan.Zero ? (int)timeline.Position.TotalSeconds : 0,
                IsPlaying = isPlaying,
                IsPaused = isPaused
            };

            DiagnosticLog.Write(
                $"Media snapshot: playing={snapshot.IsPlaying}, paused={snapshot.IsPaused}, title='{snapshot.Title}', artist='{snapshot.Artist}', album='{snapshot.Album}', player='{snapshot.Player}', total={snapshot.TotalSeconds}, position={snapshot.PositionSeconds}.");

            return snapshot;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Media snapshot update failed: {ex}");
            return MediaSnapshot.Empty;
        }
    }

    private void UnsubscribeFromSession()
    {
        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }

            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _session = null;
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
        {
            return "0:00";
        }

        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }
}
