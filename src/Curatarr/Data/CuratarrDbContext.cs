using Curatarr.Models;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Data;

public class CuratarrDbContext(DbContextOptions<CuratarrDbContext> options) : DbContext(options)
{
    public DbSet<Series> Series => Set<Series>();

    public DbSet<Episode> Episodes => Set<Episode>();

    public DbSet<EpisodeFile> EpisodeFiles => Set<EpisodeFile>();

    public DbSet<OrphanedDestinationFile> OrphanedDestinationFiles => Set<OrphanedDestinationFile>();

    public DbSet<SubtitleFile> SubtitleFiles => Set<SubtitleFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Series>(b =>
        {
            b.HasIndex(x => x.SonarrId).IsUnique();
            b.Property(x => x.Title).HasMaxLength(500);
            b.Property(x => x.Path).HasMaxLength(1000);
        });

        modelBuilder.Entity<Episode>(b =>
        {
            b.HasIndex(x => x.SonarrId).IsUnique();
            b.HasIndex(x => new { x.SeriesId, x.SeasonNumber, x.EpisodeNumber });
            b.Property(x => x.Title).HasMaxLength(500);
            b.Ignore(x => x.SourceFile);
            b.Ignore(x => x.DestinationFile);

            b.HasOne(x => x.Series)
                .WithMany(s => s.Episodes)
                .HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EpisodeFile>(b =>
        {
            b.HasIndex(x => new { x.EpisodeId, x.Side }).IsUnique();
            b.Property(x => x.RelativePath).HasMaxLength(1000);
            b.Property(x => x.Quality).HasMaxLength(100);

            b.HasOne(x => x.Episode)
                .WithMany(e => e.Files)
                .HasForeignKey(x => x.EpisodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrphanedDestinationFile>(b =>
        {
            b.HasIndex(x => new { x.SeriesId, x.RelativePath }).IsUnique();
            b.Property(x => x.RelativePath).HasMaxLength(1000);

            b.HasOne(x => x.Series)
                .WithMany(s => s.OrphanedDestinationFiles)
                .HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubtitleFile>(b =>
        {
            b.HasIndex(x => new { x.EpisodeId, x.Side, x.Suffix }).IsUnique();
            b.Property(x => x.Suffix).HasMaxLength(50);
            b.Property(x => x.RelativePath).HasMaxLength(1000);

            b.HasOne(x => x.Episode)
                .WithMany(e => e.Subtitles)
                .HasForeignKey(x => x.EpisodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
