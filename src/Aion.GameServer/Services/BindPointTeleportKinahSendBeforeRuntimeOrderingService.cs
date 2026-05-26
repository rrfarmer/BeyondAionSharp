namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahSendBeforeRuntimeOrderingStatus
{
	StoppedBeforePersistence,
	StoppedBeforePacketIntent,
	StoppedMissingSendResult,
	StoppedSendFailed,
	AwaitingRuntimeCallback,
	ReadyForRuntimeCallback,
}

public enum BindPointTeleportKinahSendBeforeRuntimeOrderingStep
{
	CheckPersistenceDecision,
	CreateInventoryUpdatePacketIntent,
	SendInventoryUpdatePacket,
	StoreCooldown,
	BroadcastCooldown,
	ScheduleFinalTeleport,
	CreateFinalMovementIntent,
}

public sealed record BindPointTeleportKinahSendBeforeRuntimeOrderingPlan(
	BindPointTeleportKinahSendBeforeRuntimeOrderingStatus Status,
	BindPointTeleportKinahPersistenceDecision PersistenceDecision,
	BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan,
	BindPointTeleportKinahInventorySendResult? SendResult,
	BindPointTeleportRuntimeCallbackExecutionResult? RuntimeResult,
	IReadOnlyList<BindPointTeleportKinahSendBeforeRuntimeOrderingStep> Steps,
	bool ShouldSendInventoryUpdatePacket,
	bool ShouldStoreCooldown,
	bool ShouldBroadcastCooldown,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahSendBeforeRuntimeOrderingService
{
	public static BindPointTeleportKinahSendBeforeRuntimeOrderingPlan CreatePlan(
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		BindPointTeleportKinahInventorySendResult? sendResult,
		BindPointTeleportRuntimeCallbackExecutionResult? runtimeResult)
	{
		// Java parity: Storage.decreaseItemCount sends SM_INVENTORY_UPDATE_ITEM before
		// BindPointTeleportService.addCooldown, action 3 broadcast, and final movement scheduling.
		// This is a non-live ordering gate only; it does not send, broadcast, persist, or move.
		if (persistenceDecision.Status != BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence)
		{
			return Stop(
				BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedBeforePersistence,
				persistenceDecision,
				packetPlan,
				sendResult: null,
				runtimeResult: null,
				[BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision],
				"Scheduled bind-point Kinah callback cannot reach packet send or runtime callback before persistence decision continues");
		}

		if (packetPlan.Status != BindPointTeleportKinahInventoryUpdatePacketPlanStatus.PacketReady
			|| !packetPlan.ShouldSendPacket
			|| packetPlan.Packet == null)
		{
			return Stop(
				BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedBeforePacketIntent,
				persistenceDecision,
				packetPlan,
				sendResult: null,
				runtimeResult: null,
				[
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision,
				],
				"Scheduled bind-point Kinah callback has no SM_INVENTORY_UPDATE_ITEM intent, so cooldown/action 3 runtime metadata stays blocked");
		}

		if (sendResult == null || sendResult.Status == BindPointTeleportKinahInventorySendStatus.MissingConnection)
		{
			return Stop(
				BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedMissingSendResult,
				persistenceDecision,
				packetPlan,
				sendResult,
				runtimeResult: null,
				[
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision,
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateInventoryUpdatePacketIntent,
				],
				"Scheduled bind-point Kinah callback must observe a sent inventory update packet before cooldown/action 3 runtime metadata may continue");
		}

		if (sendResult.Status != BindPointTeleportKinahInventorySendStatus.Sent || !sendResult.SentPacket)
		{
			return Stop(
				BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.StoppedSendFailed,
				persistenceDecision,
				packetPlan,
				sendResult,
				runtimeResult: null,
				[
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision,
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateInventoryUpdatePacketIntent,
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.SendInventoryUpdatePacket,
				],
				"Scheduled bind-point Kinah inventory update packet did not send successfully, so cooldown/action 3 fanout and movement metadata are blocked");
		}

		if (runtimeResult == null
			|| runtimeResult.Status != BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast
			|| !runtimeResult.StoredCooldownFact
			|| !runtimeResult.BroadcastCooldown)
		{
			return Stop(
				BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.AwaitingRuntimeCallback,
				persistenceDecision,
				packetPlan,
				sendResult,
				runtimeResult,
				[
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision,
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateInventoryUpdatePacketIntent,
					BindPointTeleportKinahSendBeforeRuntimeOrderingStep.SendInventoryUpdatePacket,
				],
				"Scheduled bind-point Kinah packet send succeeded, but cooldown/action 3 runtime callback metadata has not completed");
		}

		return new BindPointTeleportKinahSendBeforeRuntimeOrderingPlan(
			BindPointTeleportKinahSendBeforeRuntimeOrderingStatus.ReadyForRuntimeCallback,
			persistenceDecision,
			packetPlan,
			sendResult,
			runtimeResult,
			[
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CheckPersistenceDecision,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateInventoryUpdatePacketIntent,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.SendInventoryUpdatePacket,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.StoreCooldown,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.BroadcastCooldown,
				BindPointTeleportKinahSendBeforeRuntimeOrderingStep.ScheduleFinalTeleport,
				.. runtimeResult.ShouldTeleport
					? [BindPointTeleportKinahSendBeforeRuntimeOrderingStep.CreateFinalMovementIntent]
					: Array.Empty<BindPointTeleportKinahSendBeforeRuntimeOrderingStep>(),
			],
			ShouldSendInventoryUpdatePacket: true,
			ShouldStoreCooldown: true,
			ShouldBroadcastCooldown: true,
			runtimeResult.ShouldScheduleFinalTeleport,
			runtimeResult.ShouldTeleport,
			"Scheduled bind-point Kinah metadata is ordered like Java: saved Kinah -> SM_INVENTORY_UPDATE_ITEM sent -> addCooldown -> action 3 fanout -> final movement gate",
			IsLive: false);
	}

	private static BindPointTeleportKinahSendBeforeRuntimeOrderingPlan Stop(
		BindPointTeleportKinahSendBeforeRuntimeOrderingStatus status,
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		BindPointTeleportKinahInventorySendResult? sendResult,
		BindPointTeleportRuntimeCallbackExecutionResult? runtimeResult,
		IReadOnlyList<BindPointTeleportKinahSendBeforeRuntimeOrderingStep> steps,
		string javaSource)
	{
		return new BindPointTeleportKinahSendBeforeRuntimeOrderingPlan(
			status,
			persistenceDecision,
			packetPlan,
			sendResult,
			runtimeResult,
			steps,
			ShouldSendInventoryUpdatePacket: false,
			ShouldStoreCooldown: false,
			ShouldBroadcastCooldown: false,
			ShouldScheduleFinalTeleport: false,
			ShouldTeleport: false,
			javaSource,
			IsLive: false);
	}
}
