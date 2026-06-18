using Curatarr.Services.Subtitle;

namespace Curatarr.Models;

public class SubtitleFile
{
    public int Id { get; set; }

    public int EpisodeId { get; set; }

    public Episode Episode { get; set; } = null!;

    public FileSide Side { get; set; }

    public required string Suffix { get; set; }

    public required string RelativePath { get; set; }

    public long SizeBytes { get; set; }

    public SubtitleOrigin Origin { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
