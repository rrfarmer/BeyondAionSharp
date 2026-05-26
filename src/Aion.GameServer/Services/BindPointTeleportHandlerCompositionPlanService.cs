using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportHandlerCompositionPlanStatus
{
	NoActionDeadPlayer,
	NoActionUnknownAction,
	TeleportNeedsOperationFacts,
	TeleportBlocked,
	TeleportReady,
	TeleportReadyWithCallbackMetadata,
	CancelNeedsControlFacts,
	CancelNoAction,
	CancelReady,
}

public enum BindPointTeleportHandlerCompositionPlanStep
{
	ReadParsedClientPacketValues,
	CreateClientActionPlan,
	ComposeRequestPlan,
	AttachScheduledCallbackMetadata,
}

public sealed record BindPointTeleportHandlerCompositionPlan(
	BindPointTeleportHandlerCompositionPlanStatus Status,
	byte Action,
	int LocId,
	long Kinah,
	int PlayerObjectId,
	BindPointTeleportClientActionPlan ActionPlan,
	BindPointTeleportRequestPlan RequestPlan,
	BindPointTeleportScheduledCallbackPlan? ScheduledCallbackPlan,
	IReadOnlyList<BindPointTeleportHandlerCompositionPlanStep> Steps,
	IReadOnlyList<GameServerPacket> PacketIntents,
	IReadOnlyList<BindPointTeleportFanoutPlan> FanoutPlans,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportHandlerCompositionPlanService
{
	public static BindPointTeleportHandlerCompositionPlan CreatePlan(
		byte action,
		int locId,
		long kinah,
		int playerObjectId,
		bool playerIsDead,
		BindPointTeleportOperationPlan? operationPlan = null,
		BindPointTeleportControlPlan? controlPlan = null,
		BindPointTeleportScheduledCallbackPlan? scheduledCallbackPlan = null)
	{
		// Java parity: CM_BIND_POINT_TELEPORT.runImpl obtains active player, checks player.isDead(),
		// then dispatches action 1/2 to BindPointTeleportService. This bridge only composes existing
		// non-live plans for a future handler; it does not send packets, schedule tasks, mutate inventory,
		// store cooldowns, fan out, or move the player.
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(action, locId, kinah, playerIsDead);
		var requestPlan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId,
			operationPlan,
			controlPlan);

		var canAttachCallback = requestPlan.Status == BindPointTeleportRequestPlanStatus.TeleportReady
			&& scheduledCallbackPlan != null;
		var steps = new List<BindPointTeleportHandlerCompositionPlanStep>
		{
			BindPointTeleportHandlerCompositionPlanStep.ReadParsedClientPacketValues,
			BindPointTeleportHandlerCompositionPlanStep.CreateClientActionPlan,
			BindPointTeleportHandlerCompositionPlanStep.ComposeRequestPlan,
		};
		if (canAttachCallback)
			steps.Add(BindPointTeleportHandlerCompositionPlanStep.AttachScheduledCallbackMetadata);

		return new BindPointTeleportHandlerCompositionPlan(
			MapStatus(requestPlan.Status, canAttachCallback),
			action,
			locId,
			kinah,
			playerObjectId,
			actionPlan,
			requestPlan,
			canAttachCallback ? scheduledCallbackPlan : null,
			steps,
			requestPlan.PacketIntents,
			requestPlan.FanoutPlans,
			ShouldDispatchLiveSideEffects: false,
			"CM_BIND_POINT_TELEPORT.runImpl -> non-live handler composition bridge for action dispatch and supplied BindPointTeleportService facts",
			IsLive: false);
	}

	private static BindPointTeleportHandlerCompositionPlanStatus MapStatus(
		BindPointTeleportRequestPlanStatus status,
		bool hasScheduledCallbackMetadata)
	{
		return status switch
		{
			BindPointTeleportRequestPlanStatus.NoActionDeadPlayer => BindPointTeleportHandlerCompositionPlanStatus.NoActionDeadPlayer,
			BindPointTeleportRequestPlanStatus.NoActionUnknownAction => BindPointTeleportHandlerCompositionPlanStatus.NoActionUnknownAction,
			BindPointTeleportRequestPlanStatus.TeleportNeedsOperationFacts => BindPointTeleportHandlerCompositionPlanStatus.TeleportNeedsOperationFacts,
			BindPointTeleportRequestPlanStatus.TeleportBlocked => BindPointTeleportHandlerCompositionPlanStatus.TeleportBlocked,
			BindPointTeleportRequestPlanStatus.TeleportReady when hasScheduledCallbackMetadata => BindPointTeleportHandlerCompositionPlanStatus.TeleportReadyWithCallbackMetadata,
			BindPointTeleportRequestPlanStatus.TeleportReady => BindPointTeleportHandlerCompositionPlanStatus.TeleportReady,
			BindPointTeleportRequestPlanStatus.CancelNeedsControlFacts => BindPointTeleportHandlerCompositionPlanStatus.CancelNeedsControlFacts,
			BindPointTeleportRequestPlanStatus.CancelNoAction => BindPointTeleportHandlerCompositionPlanStatus.CancelNoAction,
			BindPointTeleportRequestPlanStatus.CancelReady => BindPointTeleportHandlerCompositionPlanStatus.CancelReady,
			_ => BindPointTeleportHandlerCompositionPlanStatus.NoActionUnknownAction,
		};
	}
}
