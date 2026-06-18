namespace Curatarr.Models;

public class MovieSubtitleFile
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    public FileSide Side { get; set; }

    public required string Suffix { get; set; }

    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
