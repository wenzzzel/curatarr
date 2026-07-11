using Curatarr.Services.Diff;
using Prometheus;

namespace Curatarr.Services.Metrics;

public static class CuratarrMetrics
{
    private static readonly Gauge Series = Prometheus.Metrics.CreateGauge(
        "curatarr_series",
        "Series counts bucketed by reconciliation status.",
        new GaugeConfiguration { LabelNames = ["status"] });

    private static readonly Gauge Episodes = Prometheus.Metrics.CreateGauge(
        "curatarr_episodes",
        "Episode counts bucketed by reconciliation status.",
        new GaugeConfiguration { LabelNames = ["status"] });

    private static readonly Gauge SeriesOrphanedFiles = Prometheus.Metrics.CreateGauge(
        "curatarr_series_orphaned_files",
        "Orphaned file count across the series destination tree.");

    private static readonly Gauge SeriesMissingSubtitles = Prometheus.Metrics.CreateGauge(
        "curatarr_series_missing_subtitles",
        "Missing subtitle file count across the series destination tree.");

    private static readonly Gauge SeriesExcessiveSubtitles = Prometheus.Metrics.CreateGauge(
        "curatarr_series_excessive_subtitles",
        "Excessive subtitle file count across the series destination tree.");

    private static readonly Gauge Movies = Prometheus.Metrics.CreateGauge(
        "curatarr_movies",
        "Movie counts bucketed by reconciliation status.",
        new GaugeConfiguration { LabelNames = ["status"] });

    private static readonly Gauge MoviesOrphanedFiles = Prometheus.Metrics.CreateGauge(
        "curatarr_movies_orphaned_files",
        "Orphaned file count across the movie destination tree.");

    private static readonly Gauge MoviesMissingSubtitles = Prometheus.Metrics.CreateGauge(
        "curatarr_movies_missing_subtitles",
        "Missing subtitle file count across the movie destination tree.");

    private static readonly Gauge MoviesExcessiveSubtitles = Prometheus.Metrics.CreateGauge(
        "curatarr_movies_excessive_subtitles",
        "Excessive subtitle file count across the movie destination tree.");

    public static async Task RefreshAsync(
        SeriesDiffService seriesDiff,
        MovieDiffService movieDiff,
        CancellationToken ct)
    {
        var seriesRows = await seriesDiff.GetSeriesDiffAsync(ct);
        var movieRows = await movieDiff.GetMoviesDiffAsync(ct);

        Series.WithLabels("ok").Set(seriesRows.Count(r => r.IsOk));
        Series.WithLabels("orphaned").Set(seriesRows.Count(r => r.IsOrphanedFolder));
        Series.WithLabels("missing").Set(seriesRows.Count(r => r.IsMissingInDestination));
        Series.WithLabels("no_original_subs").Set(
            seriesRows.Count(r => r.InSource && r.InDestination && r.OriginalSubtitles == 0));

        Episodes.WithLabels("ok").Set(seriesRows.Sum(r => r.OkEpisodes));
        Episodes.WithLabels("missing").Set(seriesRows.Sum(r => r.MissingEpisodes));
        Episodes.WithLabels("no_original_subs").Set(seriesRows.Sum(r => r.EpisodesWithoutOriginalSubs));

        SeriesOrphanedFiles.Set(seriesRows.Sum(r => r.OrphanedFiles));
        SeriesMissingSubtitles.Set(seriesRows.Sum(r => r.MissingSubtitles));
        SeriesExcessiveSubtitles.Set(seriesRows.Sum(r => r.ExcessiveSubtitles));

        Movies.WithLabels("ok").Set(movieRows.Count(r => r.IsOk));
        Movies.WithLabels("orphaned").Set(movieRows.Count(r => r.IsOrphanedFolder));
        Movies.WithLabels("missing").Set(movieRows.Count(r => r.IsMissingInDestination));
        Movies.WithLabels("no_original_subs").Set(
            movieRows.Count(r => r.InSource && r.InDestination && r.OriginalSubtitles == 0));

        MoviesOrphanedFiles.Set(movieRows.Sum(r => r.OrphanedFiles));
        MoviesMissingSubtitles.Set(movieRows.Sum(r => r.MissingSubtitles));
        MoviesExcessiveSubtitles.Set(movieRows.Sum(r => r.ExcessiveSubtitles));
    }
}
