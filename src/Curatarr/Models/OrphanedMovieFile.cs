namespace Curatarr.Models;

public class OrphanedMovieFile
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
