using System.Diagnostics;

namespace Curatarr.Services.Scheduling;

public sealed class ScheduledTask
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<IServiceProvider, CancellationToken, Task<string>> _action;

    public ScheduledTask(
        string name,
        string description,
        TimeSpan interval,
        Func<IServiceProvider, CancellationToken, Task<string>> action)
    {
        Name = name;
        Description = description;
        Interval = interval;
        _action = action;
        NextRun = DateTimeOffset.UtcNow + interval;
    }

    public string Name { get; }
    public string Description { get; }
    public TimeSpan Interval { get; }

    public DateTimeOffset NextRun { get; private set; }
    public DateTimeOffset? LastRun { get; private set; }
    public TimeSpan? LastDuration { get; private set; }
    public bool LastSucceeded { get; private set; }
    public string? LastMessage { get; private set; }
    public bool IsRunning { get; private set; }

    public async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        IsRunning = true;
        var start = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            LastMessage = await _action(services, ct);
            LastSucceeded = true;
        }
        catch (Exception ex)
        {
            LastMessage = ex.Message;
            LastSucceeded = false;
        }
        finally
        {
            stopwatch.Stop();
            LastRun = start;
            LastDuration = stopwatch.Elapsed;
            NextRun = DateTimeOffset.UtcNow + Interval;
            IsRunning = false;
            _gate.Release();
        }
    }
}
