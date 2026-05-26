namespace Aion.GameServer.Services;

public enum BindPointTeleportClientActionPlanStatus
{
	NoActionDeadPlayer,
	TeleportRequested,
	CancelRequested,
	NoActionUnknownAction,
}

public enum BindPointTeleportClientActionStep
{
	CheckDeadPlayer,
	DispatchTeleport,
	DispatchCancel,
	NoopUnknownAction,
}

public sealed record BindPointTeleportClientActionPlan(
	BindPointTeleportClientActionPlanStatus Status,
	byte Action,
	int LocId,
	long Kinah,
	IReadOnlyList<BindPointTeleportClientActionStep> Steps,
	string JavaSource)
{
	public bool ShouldInvokeTeleport => Status == BindPointTeleportClientActionPlanStatus.TeleportRequested;

	public bool ShouldInvokeCancel => Status == BindPointTeleportClientActionPlanStatus.CancelRequested;
}

public static class BindPointTeleportClientActionPlanService
{
	public static BindPointTeleportClientActionPlan CreatePlan(byte action, int locId, long kinah, bool playerIsDead)
	{
		// Java parity: network/aion/clientpackets/CM_BIND_POINT_TELEPORT.runImpl.
		if (playerIsDead)
		{
			return new BindPointTeleportClientActionPlan(
				BindPointTeleportClientActionPlanStatus.NoActionDeadPlayer,
				action,
				locId,
				kinah,
				[BindPointTeleportClientActionStep.CheckDeadPlayer],
				"CM_BIND_POINT_TELEPORT.runImpl -> if (player.isDead()) return");
		}

		return action switch
		{
			1 => new BindPointTeleportClientActionPlan(
				BindPointTeleportClientActionPlanStatus.TeleportRequested,
				action,
				locId,
				kinah,
				[BindPointTeleportClientActionStep.CheckDeadPlayer, BindPointTeleportClientActionStep.DispatchTeleport],
				"CM_BIND_POINT_TELEPORT.runImpl -> action 1 -> BindPointTeleportService.teleport(player, locId, kinah)"),
			2 => new BindPointTeleportClientActionPlan(
				BindPointTeleportClientActionPlanStatus.CancelRequested,
				action,
				locId,
				kinah,
				[BindPointTeleportClientActionStep.CheckDeadPlayer, BindPointTeleportClientActionStep.DispatchCancel],
				"CM_BIND_POINT_TELEPORT.runImpl -> action 2 -> BindPointTeleportService.cancelTeleport(player, locId)"),
			_ => new BindPointTeleportClientActionPlan(
				BindPointTeleportClientActionPlanStatus.NoActionUnknownAction,
				action,
				locId,
				kinah,
				[BindPointTeleportClientActionStep.CheckDeadPlayer, BindPointTeleportClientActionStep.NoopUnknownAction],
				"CM_BIND_POINT_TELEPORT.runImpl -> switch default no-op"),
		};
	}
}
