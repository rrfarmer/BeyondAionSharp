using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class InstanceEmptyInstanceCheckerService
{
	public static readonly TimeSpan JavaInitialDelay = TimeSpan.FromSeconds(60);
	public static readonly TimeSpan JavaPeriod = TimeSpan.FromSeconds(60);
	public static readonly TimeSpan JavaDestroyComparisonGrace = TimeSpan.FromSeconds(1);
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly InstanceDestroyWorkflowService _destroyWorkflow;

	public InstanceEmptyInstanceCheckerService(
		ThreadPoolManager threadPoolManager,
		InstanceDestroyWorkflowService destroyWorkflow)
	{
		_threadPoolManager = threadPoolManager;
		_destroyWorkflow = destroyWorkflow;
	}

	public InstanceEmptyInstanceSchedulePlan Schedule(
		int worldId,
		WorldMapInstanceRuntimeState instance,
		TimeSpan destroyDelay,
		Func<WorldMapInstanceRuntimeState, bool>? registeredTeamDisbanded = null,
		DateTimeOffset? taskStartTime = null,
		TimeSpan? initialDelay = null,
		TimeSpan? period = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(instance);
		var checkerTaskStartTime = taskStartTime ?? DateTimeOffset.UtcNow;
		var scheduledTask = _threadPoolManager.ScheduleAtFixedRateTask(
			_ =>
			{
				var plan = CreateCheckPlan(
					instance,
					checkerTaskStartTime,
					DateTimeOffset.UtcNow,
					destroyDelay,
					registeredTeamDisbanded?.Invoke(instance) ?? false);
				if (plan.ShouldDestroy)
					_destroyWorkflow.DestroyInstance(worldId, instance.InstanceId);

				return ValueTask.CompletedTask;
			},
			initialDelay ?? JavaInitialDelay,
			period ?? JavaPeriod,
			cancellationToken);
		instance.SetEmptyInstanceTask(scheduledTask);
		return new InstanceEmptyInstanceSchedulePlan(
			worldId,
			instance.InstanceId,
			checkerTaskStartTime,
			initialDelay ?? JavaInitialDelay,
			period ?? JavaPeriod,
			scheduledTask,
			"InstanceService.getNextAvailableInstance -> ThreadPoolManager.scheduleAtFixedRate(new EmptyInstanceCheckerTask(instance), 60000, 60000)");
	}

	public static InstanceEmptyInstanceCheckPlan CreateCheckPlan(
		WorldMapInstanceRuntimeState instance,
		DateTimeOffset taskStartTime,
		DateTimeOffset now,
		TimeSpan destroyDelay,
		bool registeredTeamDisbanded = false)
	{
		ArgumentNullException.ThrowIfNull(instance);
		if (instance.PlayerCount > 0)
		{
			return new InstanceEmptyInstanceCheckPlan(
				InstanceEmptyInstanceCheckStatus.PlayersInside,
				ShouldDestroy: false,
				taskStartTime,
				instance.LastPlayerLeaveTime,
				null,
				"EmptyInstanceCheckerTask.canDestroyInstance -> !worldMapInstance.getPlayersInside().isEmpty() -> false");
		}

		if (instance.IsPersonal)
		{
			return new InstanceEmptyInstanceCheckPlan(
				InstanceEmptyInstanceCheckStatus.PersonalInstance,
				ShouldDestroy: true,
				taskStartTime,
				instance.LastPlayerLeaveTime,
				null,
				"EmptyInstanceCheckerTask.canDestroyInstance -> worldMapInstance.isPersonal() -> true");
		}

		if (registeredTeamDisbanded)
		{
			return new InstanceEmptyInstanceCheckPlan(
				InstanceEmptyInstanceCheckStatus.RegisteredTeamDisbanded,
				ShouldDestroy: true,
				taskStartTime,
				instance.LastPlayerLeaveTime,
				null,
				"EmptyInstanceCheckerTask.canDestroyInstance -> registeredTeam != null && registeredTeam.isDisbanded()");
		}

		var lastActivity = taskStartTime >= instance.LastPlayerLeaveTime
			? taskStartTime
			: instance.LastPlayerLeaveTime;
		var destroyTime = lastActivity + destroyDelay;
		var shouldDestroy = now > destroyTime - JavaDestroyComparisonGrace;
		return new InstanceEmptyInstanceCheckPlan(
			shouldDestroy
				? InstanceEmptyInstanceCheckStatus.DestroyDelayElapsed
				: InstanceEmptyInstanceCheckStatus.WaitingForDestroyDelay,
			shouldDestroy,
			taskStartTime,
			instance.LastPlayerLeaveTime,
			destroyTime,
			"EmptyInstanceCheckerTask.canDestroyInstance -> System.currentTimeMillis() > calculateDestroyTime() - 1000");
	}
}

public sealed record InstanceEmptyInstanceSchedulePlan(
	int WorldId,
	int InstanceId,
	DateTimeOffset TaskStartTime,
	TimeSpan InitialDelay,
	TimeSpan Period,
	ScheduledTask ScheduledTask,
	string JavaSource);

public sealed record InstanceEmptyInstanceCheckPlan(
	InstanceEmptyInstanceCheckStatus Status,
	bool ShouldDestroy,
	DateTimeOffset TaskStartTime,
	DateTimeOffset LastPlayerLeaveTime,
	DateTimeOffset? DestroyTime,
	string JavaSource);

public enum InstanceEmptyInstanceCheckStatus
{
	PlayersInside,
	PersonalInstance,
	RegisteredTeamDisbanded,
	WaitingForDestroyDelay,
	DestroyDelayElapsed,
}
