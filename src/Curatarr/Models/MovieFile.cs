namespace Curatarr.Models;

public class MovieFile
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    public FileSide Side { get; set; }

    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public string? Quality { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
