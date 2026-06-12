namespace Curatarr.Models;

public class Movie
{
    public int Id { get; set; }

    public int RadarrId { get; set; }

    public required string Title { get; set; }

    public string? Path { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }

    public List<MovieFile> Files { get; set; } = [];

    public List<MovieSubtitleFile> Subtitles { get; set; } = [];

    public List<OrphanedMovieFile> OrphanedDestinationFiles { get; set; } = [];

    public MovieFile? SourceFile => Files.FirstOrDefault(f => f.Side == FileSide.Source);

    public MovieFile? DestinationFile => Files.FirstOrDefault(f => f.Side == FileSide.Destination);
}
