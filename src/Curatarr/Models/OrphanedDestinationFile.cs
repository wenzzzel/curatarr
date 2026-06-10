namespace Curatarr.Models;

public class OrphanedDestinationFile
{
    public int Id { get; set; }

    public int SeriesId { get; set; }

    public Series Series { get; set; } = null!;

    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
