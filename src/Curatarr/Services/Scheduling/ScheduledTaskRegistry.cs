namespace Curatarr.Services.Scheduling;

public sealed class ScheduledTaskRegistry(IEnumerable<ScheduledTask> tasks)
{
    public IReadOnlyList<ScheduledTask> Tasks { get; } = tasks.ToList();
}
