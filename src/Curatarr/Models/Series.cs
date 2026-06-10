namespace Curatarr.Models;

public class Series
{
    public int Id { get; set; }

    public int SonarrId { get; set; }

    public required string Title { get; set; }

    public string? Path { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }

    public List<Episode> Episodes { get; set; } = [];

    public List<OrphanedDestinationFile> OrphanedDestinationFiles { get; set; } = [];
}
