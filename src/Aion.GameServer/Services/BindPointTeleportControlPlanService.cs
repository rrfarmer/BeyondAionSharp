using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum BindPointTeleportControlPlanStatus
{
	NoAction,
	CancelTeleport,
	BroadcastLoginCooldown,
}

public enum BindPointTeleportControlStep
{
	CancelSkillUseTask,
	BroadcastCancel,
	BroadcastCooldownAndReceive,
}

public sealed record BindPointTeleportControlPlan(
	BindPointTeleportControlPlanStatus Status,
	bool ShouldCancelSkillUseTask,
	bool ShouldBroadcast,
	int PlayerObjectId,
	int LocId,
	int CooldownTimeLeftSeconds,
	SmBindPointTeleport? Packet,
	IReadOnlyList<BindPointTeleportControlStep> Steps,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportControlPlanService
{
	public static BindPointTeleportControlPlan CreateCancelPlan(int playerObjectId, int locId, bool hasSkillUseTask)
	{
		// Java parity: services/teleport/BindPointTeleportService.cancelTeleport.
		// This only records cancel/broadcast intent; it does not touch the live controller task map.
		if (!hasSkillUseTask)
			return NoAction(playerObjectId, locId, "BindPointTeleportService.cancelTeleport -> no TaskId.SKILL_USE");

		return new BindPointTeleportControlPlan(
			BindPointTeleportControlPlanStatus.CancelTeleport,
			ShouldCancelSkillUseTask: true,
			ShouldBroadcast: true,
			playerObjectId,
			locId,
			CooldownTimeLeftSeconds: 0,
			SmBindPointTeleport.Cancel(playerObjectId, locId),
			[BindPointTeleportControlStep.CancelSkillUseTask, BindPointTeleportControlStep.BroadcastCancel],
			"BindPointTeleportService.cancelTeleport -> cancel TaskId.SKILL_USE -> SM_BIND_POINT_TELEPORT(action=2)",
			IsLive: false);
	}

	public static BindPointTeleportControlPlan CreateLoginCooldownPlan(
		int playerObjectId,
		int locId,
		int cooldownTimeLeftSeconds)
	{
		// Java parity: services/teleport/BindPointTeleportService.onLogin.
		// Cooldown lookup/time math stays outside this scalar fact planner.
		if (cooldownTimeLeftSeconds <= 0)
			return NoAction(playerObjectId, locId, "BindPointTeleportService.onLogin -> no active cooldown", cooldownTimeLeftSeconds);

		return new BindPointTeleportControlPlan(
			BindPointTeleportControlPlanStatus.BroadcastLoginCooldown,
			ShouldCancelSkillUseTask: false,
			ShouldBroadcast: true,
			playerObjectId,
			locId,
			cooldownTimeLeftSeconds,
			SmBindPointTeleport.Cooldown(playerObjectId, locId, cooldownTimeLeftSeconds),
			[BindPointTeleportControlStep.BroadcastCooldownAndReceive],
			"BindPointTeleportService.onLogin -> SM_BIND_POINT_TELEPORT(action=3)",
			IsLive: false);
	}

	private static BindPointTeleportControlPlan NoAction(
		int playerObjectId,
		int locId,
		string javaSource,
		int cooldownTimeLeftSeconds = 0)
	{
		return new BindPointTeleportControlPlan(
			BindPointTeleportControlPlanStatus.NoAction,
			ShouldCancelSkillUseTask: false,
			ShouldBroadcast: false,
			playerObjectId,
			locId,
			cooldownTimeLeftSeconds,
			Packet: null,
			[],
			javaSource,
			IsLive: false);
	}
}
