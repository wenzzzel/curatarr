namespace Curatarr.Models;

public class EpisodeFile
{
    public int Id { get; set; }

    public int EpisodeId { get; set; }

    public Episode Episode { get; set; } = null!;

    public FileSide Side { get; set; }

    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public string? Quality { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
