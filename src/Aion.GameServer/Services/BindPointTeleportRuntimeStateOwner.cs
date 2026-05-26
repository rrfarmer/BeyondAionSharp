using System.Collections.Concurrent;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public enum BindPointTeleportRuntimeTaskOwnerStatus
{
	ScheduledNewTask,
	ReplacedExistingTask,
	CancelledExistingTask,
	NoTaskToCancel,
	ClearedPlayer,
}

public sealed record BindPointTeleportRuntimeTaskOwnerResult(
	BindPointTeleportRuntimeTaskOwnerStatus Status,
	int PlayerObjectId,
	int? LocId,
	bool HadExistingTask,
	bool CancelledExistingTask,
	bool ScheduledTask,
	bool RemovedTask,
	BindPointTeleportSkillUseTaskPlan Plan,
	bool IsLive);

public sealed class BindPointTeleportRuntimeStateOwner
{
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly ConcurrentDictionary<int, ScheduledSkillUseTask> _skillUseTasks = new();
	private readonly ConcurrentDictionary<int, BindPointTeleportCooldownFact> _cooldowns = new();

	public BindPointTeleportRuntimeStateOwner(ThreadPoolManager threadPoolManager)
	{
		_threadPoolManager = threadPoolManager;
	}

	public int PendingSkillUseTaskCount => _skillUseTasks.Count;

	public int CooldownCount => _cooldowns.Count;

	public BindPointTeleportRuntimeTaskOwnerResult ScheduleSkillUseTask(
		int playerObjectId,
		int locId,
		Func<CancellationToken, ValueTask> callback,
		TimeSpan? delay = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: BindPointTeleportService.teleport stores a ThreadPoolManager task in TaskId.SKILL_USE.
		var hasExistingTask = HasSkillUseTask(playerObjectId);
		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager.Schedule(
			async taskCancellationToken =>
			{
				await callback(taskCancellationToken);
			},
			delay ?? TimeSpan.FromMilliseconds(BindPointTeleportRuntimeStatePlanService.SkillUseDelayMilliseconds),
			cancellationToken);
		var entry = new ScheduledSkillUseTask(locId, scheduledTask);
		var cancelledExistingTask = false;
		_skillUseTasks.AddOrUpdate(
			playerObjectId,
			entry,
			(_, oldEntry) =>
			{
				// Java parity: CreatureController.addTask cancels the old Future with cancel(false) before replacement.
				cancelledExistingTask = oldEntry.Task.Cancel();
				return entry;
			});

		var plan = BindPointTeleportRuntimeStatePlanService.CreateScheduleSkillUseTaskPlan(
			playerObjectId,
			locId,
			hasExistingTask);
		return new BindPointTeleportRuntimeTaskOwnerResult(
			hasExistingTask
				? BindPointTeleportRuntimeTaskOwnerStatus.ReplacedExistingTask
				: BindPointTeleportRuntimeTaskOwnerStatus.ScheduledNewTask,
			playerObjectId,
			locId,
			hasExistingTask,
			cancelledExistingTask,
			ScheduledTask: true,
			RemovedTask: false,
			plan,
			IsLive: true);
	}

	public bool HasSkillUseTask(int playerObjectId)
	{
		// Java parity: BindPointTeleportService.cancelTeleport uses hasTask(TaskId.SKILL_USE), which checks slot presence.
		return _skillUseTasks.ContainsKey(playerObjectId);
	}

	public BindPointTeleportRuntimeTaskOwnerResult CancelSkillUseTask(int playerObjectId, int locId)
	{
		// Java parity: CreatureController.cancelTask removes the task before calling Future.cancel(false).
		if (!_skillUseTasks.TryRemove(playerObjectId, out var entry))
		{
			var noTaskPlan = BindPointTeleportRuntimeStatePlanService.CreateCancelSkillUseTaskPlan(
				playerObjectId,
				locId,
				hasExistingSkillUseTask: false);
			return new BindPointTeleportRuntimeTaskOwnerResult(
				BindPointTeleportRuntimeTaskOwnerStatus.NoTaskToCancel,
				playerObjectId,
				locId,
				HadExistingTask: false,
				CancelledExistingTask: false,
				ScheduledTask: false,
				RemovedTask: false,
				noTaskPlan,
				IsLive: true);
		}

		var cancelled = entry.Task.Cancel();
		var plan = BindPointTeleportRuntimeStatePlanService.CreateCancelSkillUseTaskPlan(
			playerObjectId,
			locId,
			hasExistingSkillUseTask: true);
		return new BindPointTeleportRuntimeTaskOwnerResult(
			BindPointTeleportRuntimeTaskOwnerStatus.CancelledExistingTask,
			playerObjectId,
			locId,
			HadExistingTask: true,
			cancelled,
			ScheduledTask: false,
			RemovedTask: true,
			plan,
			IsLive: true);
	}

	public BindPointTeleportRuntimeTaskOwnerResult ClearPlayer(int playerObjectId)
	{
		// Java parity: CreatureController.cancelAllTasks cancels tasks when the controller is deleted.
		if (!_skillUseTasks.TryRemove(playerObjectId, out var entry))
		{
			var noTaskPlan = BindPointTeleportRuntimeStatePlanService.CreateCancelSkillUseTaskPlan(
				playerObjectId,
				locId: 0,
				hasExistingSkillUseTask: false);
			return new BindPointTeleportRuntimeTaskOwnerResult(
				BindPointTeleportRuntimeTaskOwnerStatus.NoTaskToCancel,
				playerObjectId,
				LocId: null,
				HadExistingTask: false,
				CancelledExistingTask: false,
				ScheduledTask: false,
				RemovedTask: false,
				noTaskPlan,
				IsLive: true);
		}

		var cancelled = entry.Task.Cancel();
		var plan = BindPointTeleportRuntimeStatePlanService.CreateCancelSkillUseTaskPlan(
			playerObjectId,
			entry.LocId,
			hasExistingSkillUseTask: true);
		return new BindPointTeleportRuntimeTaskOwnerResult(
			BindPointTeleportRuntimeTaskOwnerStatus.ClearedPlayer,
			playerObjectId,
			entry.LocId,
			HadExistingTask: true,
			cancelled,
			ScheduledTask: false,
			RemovedTask: true,
			plan,
			IsLive: true);
	}

	public BindPointTeleportCooldownFact AddCooldown(
		int playerObjectId,
		int locId,
		long currentTimeMillis)
	{
		var plan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId,
			locId,
			currentTimeMillis);
		var fact = new BindPointTeleportCooldownFact(
			playerObjectId,
			locId,
			plan.CooldownEndMillis.GetValueOrDefault());
		_cooldowns[playerObjectId] = fact;
		return fact;
	}

	public BindPointTeleportCooldownFact? GetCooldown(int playerObjectId)
	{
		// Java parity: BindPointTeleportService.getCooldown reads the static player-id keyed map.
		return _cooldowns.TryGetValue(playerObjectId, out var cooldown)
			? cooldown
			: null;
	}

	public BindPointTeleportCooldownPlan CreateLookupCooldownPlan(
		int playerObjectId,
		long currentTimeMillis)
	{
		return BindPointTeleportRuntimeStatePlanService.CreateLookupCooldownPlan(
			playerObjectId,
			GetCooldown(playerObjectId),
			currentTimeMillis);
	}

	private sealed record ScheduledSkillUseTask(int LocId, ScheduledTask Task);
}
