namespace WindowRPC.Models;

internal sealed class MediaSnapshot : IEquatable<MediaSnapshot>
{
    public static MediaSnapshot Empty { get; } = new();

    public string Title { get; init; } = "No media playing";

    public string Artist { get; init; } = "Unknown artist";

    public string Album { get; init; } = "Unknown album";

    public string Total { get; init; } = "0:00";

    public string Position { get; init; } = "0:00";

    public string Player { get; init; } = string.Empty;

    public int TotalSeconds { get; init; }

    public int PositionSeconds { get; init; }

    public bool IsPlaying { get; init; }

    public bool IsPaused { get; init; }

    public bool IsActive => IsPlaying || IsPaused;

    public bool IsOverrideEligible => IsPlaying;

    public bool Equals(MediaSnapshot? other)
    {
        if (other is null)
        {
            return false;
        }

        return Title == other.Title
            && Artist == other.Artist
            && Album == other.Album
            && Total == other.Total
            && Position == other.Position
            && Player == other.Player
            && TotalSeconds == other.TotalSeconds
            && PositionSeconds == other.PositionSeconds
            && IsPlaying == other.IsPlaying
            && IsPaused == other.IsPaused;
    }

    public override bool Equals(object? obj) => Equals(obj as MediaSnapshot);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Title);
        hash.Add(Artist);
        hash.Add(Album);
        hash.Add(Total);
        hash.Add(Position);
        hash.Add(Player);
        hash.Add(TotalSeconds);
        hash.Add(PositionSeconds);
        hash.Add(IsPlaying);
        hash.Add(IsPaused);
        return hash.ToHashCode();
    }
}
