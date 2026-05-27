using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class PlayerProtectionActiveTaskScheduledTaskHandleAdapter : IPlayerProtectionActiveTaskTaskHandle
{
	private readonly ScheduledTask _scheduledTask;

	public PlayerProtectionActiveTaskScheduledTaskHandleAdapter(ScheduledTask scheduledTask)
	{
		_scheduledTask = scheduledTask;
	}

	public bool IsDone => _scheduledTask.Completion.IsCompleted;

	public bool Cancel(bool mayInterruptIfRunning)
	{
		// Java parity: Future.cancel(false). The C# ScheduledTask cancellation is cooperative,
		// so mayInterruptIfRunning is intentionally ignored and documented as still needing runtime comparison.
		return _scheduledTask.Cancel();
	}
}
