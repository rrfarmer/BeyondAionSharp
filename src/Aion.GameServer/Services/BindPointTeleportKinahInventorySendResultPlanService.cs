namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahInventorySendStatus
{
	Sent,
	MissingConnection,
	Failed,
}

public sealed record BindPointTeleportKinahInventorySendResult(
	BindPointTeleportKinahInventorySendStatus Status,
	int PlayerObjectId,
	bool SentPacket,
	string JavaSource,
	bool IsLive);

public enum BindPointTeleportKinahInventorySendDecisionStatus
{
	StoppedBeforePacketIntent,
	StoppedMissingConnection,
	StoppedSendFailed,
	ReadyForCooldownFanout,
}

public sealed record BindPointTeleportKinahInventorySendDecision(
	BindPointTeleportKinahInventorySendDecisionStatus Status,
	BindPointTeleportKinahCallbackComposition Composition,
	BindPointTeleportKinahInventorySendResult? SendResult,
	bool ShouldContinueToCooldownFanout,
	bool ShouldStoreCooldown,
	bool ShouldBroadcastCooldown,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahInventorySendResultPlanService
{
	public static BindPointTeleportKinahInventorySendDecision CreateDecision(
		BindPointTeleportKinahCallbackComposition composition,
		BindPointTeleportKinahInventorySendResult? sendResult)
	{
		// Java parity: the inventory update packet is sent before addCooldown/action 3 fanout.
		// This planner consumes a supplied send result only; it never calls SendPacketAsync.
		if (!composition.ShouldSendInventoryUpdatePacket || composition.InventoryUpdatePacket == null)
		{
			return Stop(
				BindPointTeleportKinahInventorySendDecisionStatus.StoppedBeforePacketIntent,
				composition,
				sendResult: null,
				"Scheduled bind-point callback lacks an inventory update packet intent, so cooldown/action 3 fanout cannot continue through the send gate");
		}

		if (sendResult == null || sendResult.Status == BindPointTeleportKinahInventorySendStatus.MissingConnection)
		{
			return Stop(
				BindPointTeleportKinahInventorySendDecisionStatus.StoppedMissingConnection,
				composition,
				sendResult,
				"Scheduled bind-point Kinah inventory update packet was not sent because the player connection was missing; cooldown/action 3 fanout stays blocked");
		}

		if (sendResult.Status != BindPointTeleportKinahInventorySendStatus.Sent || !sendResult.SentPacket)
		{
			return Stop(
				BindPointTeleportKinahInventorySendDecisionStatus.StoppedSendFailed,
				composition,
				sendResult,
				"Scheduled bind-point Kinah inventory update packet send failed; cooldown/action 3 fanout stays blocked");
		}

		return new BindPointTeleportKinahInventorySendDecision(
			BindPointTeleportKinahInventorySendDecisionStatus.ReadyForCooldownFanout,
			composition,
			sendResult,
			ShouldContinueToCooldownFanout: true,
			composition.ShouldStoreCooldown,
			composition.ShouldBroadcastCooldown,
			composition.ShouldScheduleFinalTeleport,
			composition.ShouldTeleport,
			"Scheduled bind-point Kinah inventory update packet send succeeded; staged metadata may continue to addCooldown/action 3 fanout/final movement",
			IsLive: false);
	}

	private static BindPointTeleportKinahInventorySendDecision Stop(
		BindPointTeleportKinahInventorySendDecisionStatus status,
		BindPointTeleportKinahCallbackComposition composition,
		BindPointTeleportKinahInventorySendResult? sendResult,
		string javaSource)
	{
		return new BindPointTeleportKinahInventorySendDecision(
			status,
			composition,
			sendResult,
			ShouldContinueToCooldownFanout: false,
			ShouldStoreCooldown: false,
			ShouldBroadcastCooldown: false,
			ShouldScheduleFinalTeleport: false,
			ShouldTeleport: false,
			javaSource,
			IsLive: false);
	}
}
