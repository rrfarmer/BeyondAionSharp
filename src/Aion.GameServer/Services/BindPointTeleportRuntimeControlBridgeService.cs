namespace Aion.GameServer.Services;

public sealed record BindPointTeleportRuntimeControlBridgePlan(
	BindPointTeleportControlPlan ControlPlan,
	BindPointTeleportRuntimeTaskOwnerResult? TaskOwnerResult,
	BindPointTeleportCooldownPlan? CooldownPlan,
	bool ShouldSendPacket,
	bool IsLive,
	string JavaSource);

public sealed class BindPointTeleportRuntimeControlBridgeService
{
	private readonly BindPointTeleportRuntimeStateOwner _runtimeStateOwner;

	public BindPointTeleportRuntimeControlBridgeService(BindPointTeleportRuntimeStateOwner runtimeStateOwner)
	{
		_runtimeStateOwner = runtimeStateOwner;
	}

	public BindPointTeleportRuntimeControlBridgePlan CreateCancelPlan(
		int playerObjectId,
		int locId)
	{
		// Java parity: BindPointTeleportService.cancelTeleport checks hasTask(TaskId.SKILL_USE), then cancelTask, then broadcasts action 2.
		var hasSkillUseTask = _runtimeStateOwner.HasSkillUseTask(playerObjectId);
		var taskOwnerResult = hasSkillUseTask
			? _runtimeStateOwner.CancelSkillUseTask(playerObjectId, locId)
			: null;
		var controlPlan = BindPointTeleportControlPlanService.CreateCancelPlan(
			playerObjectId,
			locId,
			hasSkillUseTask);
		return new BindPointTeleportRuntimeControlBridgePlan(
			controlPlan,
			taskOwnerResult,
			CooldownPlan: null,
			controlPlan.ShouldBroadcast,
			IsLive: false,
			"BindPointTeleportService.cancelTeleport -> hasTask(TaskId.SKILL_USE) -> cancelTask(TaskId.SKILL_USE) -> broadcast action 2");
	}

	public BindPointTeleportRuntimeControlBridgePlan CreateLoginCooldownPlan(
		int playerObjectId,
		long currentTimeMillis)
	{
		// Java parity: BindPointTeleportService.onLogin reads the static cooldown map and broadcasts action 3 only when timeLeft > 0.
		var cooldownPlan = _runtimeStateOwner.CreateLookupCooldownPlan(playerObjectId, currentTimeMillis);
		var controlPlan = BindPointTeleportControlPlanService.CreateLoginCooldownPlan(
			playerObjectId,
			cooldownPlan.LocId.GetValueOrDefault(),
			cooldownPlan.TimeLeftSeconds);
		return new BindPointTeleportRuntimeControlBridgePlan(
			controlPlan,
			TaskOwnerResult: null,
			cooldownPlan,
			controlPlan.ShouldBroadcast,
			IsLive: false,
			"BindPointTeleportService.onLogin -> getCooldown(player) -> cooldown.getTimeLeft() > 0 -> broadcastPacketAndReceive action 3");
	}
}
