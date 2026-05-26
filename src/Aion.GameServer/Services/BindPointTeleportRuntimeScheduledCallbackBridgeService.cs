namespace Aion.GameServer.Services;

public enum BindPointTeleportRuntimeScheduledCallbackBridgeStatus
{
	NotScheduledOperationNotReady,
	NotScheduledMissingCallbackPlan,
	ScheduledMetadataCallback,
}

public sealed record BindPointTeleportRuntimeScheduledCallbackBridgePlan(
	BindPointTeleportRuntimeScheduledCallbackBridgeStatus Status,
	BindPointTeleportOperationPlan OperationPlan,
	BindPointTeleportScheduledCallbackPlan? CallbackPlan,
	BindPointTeleportRuntimeTaskOwnerResult? TaskOwnerResult,
	bool ShouldScheduleTask,
	bool ScheduledTask,
	string JavaSource,
	bool IsLive);

public sealed class BindPointTeleportRuntimeScheduledCallbackBridgeService
{
	private readonly BindPointTeleportRuntimeStateOwner _runtimeStateOwner;

	public BindPointTeleportRuntimeScheduledCallbackBridgeService(BindPointTeleportRuntimeStateOwner runtimeStateOwner)
	{
		_runtimeStateOwner = runtimeStateOwner;
	}

	public BindPointTeleportRuntimeScheduledCallbackBridgePlan ScheduleMetadataCallback(
		int playerObjectId,
		BindPointTeleportOperationPlan operationPlan,
		BindPointTeleportScheduledCallbackPlan? callbackPlan,
		Func<BindPointTeleportScheduledCallbackPlan, CancellationToken, ValueTask>? metadataCallback = null,
		TimeSpan? delay = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: BindPointTeleportService.teleport schedules TaskId.SKILL_USE only after the start broadcast
		// and only for a ready operation. This bridge schedules metadata only; Kinah/cooldown/fanout/movement stay unwired.
		if (!operationPlan.CanSchedule)
		{
			return NotScheduled(
				BindPointTeleportRuntimeScheduledCallbackBridgeStatus.NotScheduledOperationNotReady,
				operationPlan,
				callbackPlan,
				"BindPointTeleportService.teleport -> failed operation does not schedule TaskId.SKILL_USE");
		}

		if (callbackPlan == null)
		{
			return NotScheduled(
				BindPointTeleportRuntimeScheduledCallbackBridgeStatus.NotScheduledMissingCallbackPlan,
				operationPlan,
				callbackPlan,
				"C# staging guard: ready operation requires supplied scheduled callback metadata before using runtime owner");
		}

		var taskOwnerResult = _runtimeStateOwner.ScheduleSkillUseTask(
			playerObjectId,
			operationPlan.LocId,
			token => metadataCallback?.Invoke(callbackPlan, token) ?? ValueTask.CompletedTask,
			delay,
			cancellationToken);
		return new BindPointTeleportRuntimeScheduledCallbackBridgePlan(
			BindPointTeleportRuntimeScheduledCallbackBridgeStatus.ScheduledMetadataCallback,
			operationPlan,
			callbackPlan,
			taskOwnerResult,
			ShouldScheduleTask: true,
			ScheduledTask: true,
			"BindPointTeleportService.teleport -> player.getController().addTask(TaskId.SKILL_USE, ThreadPoolManager.schedule(..., 10000)); callback side effects remain staged",
			IsLive: false);
	}

	private static BindPointTeleportRuntimeScheduledCallbackBridgePlan NotScheduled(
		BindPointTeleportRuntimeScheduledCallbackBridgeStatus status,
		BindPointTeleportOperationPlan operationPlan,
		BindPointTeleportScheduledCallbackPlan? callbackPlan,
		string javaSource)
	{
		return new BindPointTeleportRuntimeScheduledCallbackBridgePlan(
			status,
			operationPlan,
			callbackPlan,
			TaskOwnerResult: null,
			ShouldScheduleTask: false,
			ScheduledTask: false,
			javaSource,
			IsLive: false);
	}
}
