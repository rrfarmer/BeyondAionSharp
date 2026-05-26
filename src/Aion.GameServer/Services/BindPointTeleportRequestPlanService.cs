using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportRequestPlanStatus
{
	NoActionDeadPlayer,
	NoActionUnknownAction,
	TeleportNeedsOperationFacts,
	TeleportBlocked,
	TeleportReady,
	CancelNeedsControlFacts,
	CancelNoAction,
	CancelReady,
}

public enum BindPointTeleportRequestPlanStep
{
	ReadClientActionPlan,
	ComposeTeleportOperationPlan,
	ComposeTeleportStartFanout,
	ComposeTeleportCooldownFanout,
	ComposeCancelControlPlan,
	ComposeCancelFanout,
}

public sealed record BindPointTeleportRequestPlan(
	BindPointTeleportRequestPlanStatus Status,
	BindPointTeleportClientActionPlan ActionPlan,
	BindPointTeleportOperationPlan? OperationPlan,
	BindPointTeleportControlPlan? ControlPlan,
	IReadOnlyList<BindPointTeleportRequestPlanStep> Steps,
	IReadOnlyList<GameServerPacket> PacketIntents,
	IReadOnlyList<BindPointTeleportFanoutPlan> FanoutPlans,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportRequestPlanService
{
	public static BindPointTeleportRequestPlan CreatePlan(
		BindPointTeleportClientActionPlan actionPlan,
		int playerObjectId,
		BindPointTeleportOperationPlan? operationPlan = null,
		BindPointTeleportControlPlan? controlPlan = null)
	{
		// Java parity: network/aion/clientpackets/CM_BIND_POINT_TELEPORT.runImpl dispatches to
		// services/teleport/BindPointTeleportService without performing live work in the packet itself.
		if (actionPlan.Status == BindPointTeleportClientActionPlanStatus.NoActionDeadPlayer)
		{
			return NoAction(
				BindPointTeleportRequestPlanStatus.NoActionDeadPlayer,
				actionPlan,
				operationPlan: null,
				controlPlan: null,
				"CM_BIND_POINT_TELEPORT.runImpl -> dead player returns before teleport/cancel dispatch");
		}

		if (actionPlan.Status == BindPointTeleportClientActionPlanStatus.NoActionUnknownAction)
		{
			return NoAction(
				BindPointTeleportRequestPlanStatus.NoActionUnknownAction,
				actionPlan,
				operationPlan: null,
				controlPlan: null,
				"CM_BIND_POINT_TELEPORT.runImpl -> switch default no-op");
		}

		if (actionPlan.ShouldInvokeTeleport)
			return CreateTeleportPlan(actionPlan, playerObjectId, operationPlan);

		if (actionPlan.ShouldInvokeCancel)
			return CreateCancelPlan(actionPlan, playerObjectId, controlPlan);

		return NoAction(
			BindPointTeleportRequestPlanStatus.NoActionUnknownAction,
			actionPlan,
			operationPlan: null,
			controlPlan: null,
			"CM_BIND_POINT_TELEPORT.runImpl -> no matching request action");
	}

	private static BindPointTeleportRequestPlan CreateTeleportPlan(
		BindPointTeleportClientActionPlan actionPlan,
		int playerObjectId,
		BindPointTeleportOperationPlan? operationPlan)
	{
		if (operationPlan == null)
		{
			return NoAction(
				BindPointTeleportRequestPlanStatus.TeleportNeedsOperationFacts,
				actionPlan,
				operationPlan: null,
				controlPlan: null,
				"CM_BIND_POINT_TELEPORT.runImpl action 1 -> BindPointTeleportService.teleport requires hotspot, price, and requirement facts",
				[BindPointTeleportRequestPlanStep.ReadClientActionPlan]);
		}

		var steps = new List<BindPointTeleportRequestPlanStep>
		{
			BindPointTeleportRequestPlanStep.ReadClientActionPlan,
			BindPointTeleportRequestPlanStep.ComposeTeleportOperationPlan,
		};

		if (!operationPlan.CanSchedule)
		{
			return new BindPointTeleportRequestPlan(
				BindPointTeleportRequestPlanStatus.TeleportBlocked,
				actionPlan,
				operationPlan,
				ControlPlan: null,
				steps,
				PacketIntents: operationPlan.PacketIntents,
				FanoutPlans: [],
				"CM_BIND_POINT_TELEPORT.runImpl action 1 -> BindPointTeleportService.teleport stopped before broadcast/schedule",
				IsLive: false);
		}

		var fanoutPlans = new List<BindPointTeleportFanoutPlan>();
		if (operationPlan.PacketIntents.Count > 0)
		{
			steps.Add(BindPointTeleportRequestPlanStep.ComposeTeleportStartFanout);
			fanoutPlans.Add(BindPointTeleportFanoutPlanService.CreatePlan(
				BindPointTeleportFanoutSource.TeleportStartBroadcast,
				playerObjectId,
				operationPlan.PacketIntents[0]));
		}

		if (operationPlan.PacketIntents.Count > 1)
		{
			steps.Add(BindPointTeleportRequestPlanStep.ComposeTeleportCooldownFanout);
			fanoutPlans.Add(BindPointTeleportFanoutPlanService.CreatePlan(
				BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
				playerObjectId,
				operationPlan.PacketIntents[1]));
		}

		return new BindPointTeleportRequestPlan(
			BindPointTeleportRequestPlanStatus.TeleportReady,
			actionPlan,
			operationPlan,
			ControlPlan: null,
			steps,
			operationPlan.PacketIntents,
			fanoutPlans,
			"CM_BIND_POINT_TELEPORT.runImpl action 1 -> BindPointTeleportService.teleport composed non-live operation and fanout intents",
			IsLive: false);
	}

	private static BindPointTeleportRequestPlan CreateCancelPlan(
		BindPointTeleportClientActionPlan actionPlan,
		int playerObjectId,
		BindPointTeleportControlPlan? controlPlan)
	{
		if (controlPlan == null)
		{
			return NoAction(
				BindPointTeleportRequestPlanStatus.CancelNeedsControlFacts,
				actionPlan,
				operationPlan: null,
				controlPlan: null,
				"CM_BIND_POINT_TELEPORT.runImpl action 2 -> BindPointTeleportService.cancelTeleport requires TaskId.SKILL_USE fact",
				[BindPointTeleportRequestPlanStep.ReadClientActionPlan]);
		}

		var steps = new List<BindPointTeleportRequestPlanStep>
		{
			BindPointTeleportRequestPlanStep.ReadClientActionPlan,
			BindPointTeleportRequestPlanStep.ComposeCancelControlPlan,
		};

		if (controlPlan.Status != BindPointTeleportControlPlanStatus.CancelTeleport ||
		    !controlPlan.ShouldBroadcast ||
		    controlPlan.Packet == null)
		{
			return new BindPointTeleportRequestPlan(
				BindPointTeleportRequestPlanStatus.CancelNoAction,
				actionPlan,
				OperationPlan: null,
				controlPlan,
				steps,
				PacketIntents: [],
				FanoutPlans: [],
				"CM_BIND_POINT_TELEPORT.runImpl action 2 -> BindPointTeleportService.cancelTeleport found no active TaskId.SKILL_USE",
				IsLive: false);
		}

		steps.Add(BindPointTeleportRequestPlanStep.ComposeCancelFanout);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.CancelBroadcast,
			playerObjectId,
			controlPlan.Packet);

		return new BindPointTeleportRequestPlan(
			BindPointTeleportRequestPlanStatus.CancelReady,
			actionPlan,
			OperationPlan: null,
			controlPlan,
			steps,
			PacketIntents: [controlPlan.Packet],
			FanoutPlans: [fanoutPlan],
			"CM_BIND_POINT_TELEPORT.runImpl action 2 -> BindPointTeleportService.cancelTeleport composed non-live cancel and fanout intent",
			IsLive: false);
	}

	private static BindPointTeleportRequestPlan NoAction(
		BindPointTeleportRequestPlanStatus status,
		BindPointTeleportClientActionPlan actionPlan,
		BindPointTeleportOperationPlan? operationPlan,
		BindPointTeleportControlPlan? controlPlan,
		string javaSource,
		IReadOnlyList<BindPointTeleportRequestPlanStep>? steps = null)
	{
		return new BindPointTeleportRequestPlan(
			status,
			actionPlan,
			operationPlan,
			controlPlan,
			steps ?? [BindPointTeleportRequestPlanStep.ReadClientActionPlan],
			PacketIntents: [],
			FanoutPlans: [],
			javaSource,
			IsLive: false);
	}
}
