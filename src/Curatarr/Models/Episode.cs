namespace Curatarr.Models;

public class Episode
{
    public int Id { get; set; }

    public int SonarrId { get; set; }

    public int SeriesId { get; set; }

    public Series Series { get; set; } = null!;

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset? AirDate { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }

    public List<EpisodeFile> Files { get; set; } = [];

    public List<SubtitleFile> Subtitles { get; set; } = [];

    public EpisodeFile? SourceFile => Files.FirstOrDefault(f => f.Side == FileSide.Source);

    public EpisodeFile? DestinationFile => Files.FirstOrDefault(f => f.Side == FileSide.Destination);
}
