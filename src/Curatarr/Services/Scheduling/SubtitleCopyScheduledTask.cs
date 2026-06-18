using Curatarr.Services.MovieSubtitleCopy;
using Curatarr.Services.SubtitleCopy;

namespace Curatarr.Services.Scheduling;

public static class SubtitleCopyScheduledTask
{
    public static ScheduledTask Create(TimeSpan interval) => new(
        name: "Subtitle copy",
        description: "Copy subtitle files from source to destination when missing.",
        interval: interval,
        action: RunAsync);

    private static async Task<string> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var seriesCopy = services.GetRequiredService<SubtitleCopyService>();
        var movieCopy = services.GetRequiredService<MovieSubtitleCopyService>();

        var seriesResult = await seriesCopy.RunAsync(ct);
        var movieResult = await movieCopy.RunAsync(ct);

        return $"Series: {seriesResult.FilesCopied} copied, {seriesResult.CopyFailures} failed. " +
               $"Movies: {movieResult.FilesCopied} copied, {movieResult.CopyFailures} failed.";
    }
}
