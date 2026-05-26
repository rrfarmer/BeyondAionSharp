using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahCallbackCompositionStatus
{
	StoppedBeforePersistence,
	StoppedBeforePacket,
	StoppedBeforeRuntimeCallback,
	ReadyWithRuntimeCallback,
}

public enum BindPointTeleportKinahCallbackCompositionStep
{
	CheckPersistenceDecision,
	CreateInventoryUpdatePacketIntent,
	StoreCooldown,
	BroadcastCooldown,
	ScheduleFinalTeleport,
	CreateFinalMovementIntent,
}

public sealed record BindPointTeleportKinahCallbackComposition(
	BindPointTeleportKinahCallbackCompositionStatus Status,
	BindPointTeleportKinahPersistenceDecision PersistenceDecision,
	BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan,
	BindPointTeleportRuntimeCallbackExecutionResult? RuntimeResult,
	SmInventoryUpdateItem? InventoryUpdatePacket,
	IReadOnlyList<BindPointTeleportKinahCallbackCompositionStep> Steps,
	bool ShouldSendInventoryUpdatePacket,
	bool ShouldStoreCooldown,
	bool ShouldBroadcastCooldown,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahCallbackResultCompositionService
{
	public static BindPointTeleportKinahCallbackComposition CreateComposition(
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		BindPointTeleportRuntimeCallbackExecutionResult? runtimeResult)
	{
		// Java parity: BindPointTeleportService scheduled callback orders successful side effects as
		// tryDecreaseKinah/SM_INVENTORY_UPDATE_ITEM -> addCooldown -> action 3 broadcast -> final teleport schedule.
		// This composer only joins already staged metadata; it does not send, persist, broadcast, or move.
		if (persistenceDecision.Status != BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence)
		{
			return Stop(
				BindPointTeleportKinahCallbackCompositionStatus.StoppedBeforePersistence,
				persistenceDecision,
				packetPlan,
				runtimeResult: null,
				"Scheduled bind-point callback stopped before persistence success, so packet, cooldown, fanout, and movement metadata do not continue");
		}

		if (packetPlan.Status != BindPointTeleportKinahInventoryUpdatePacketPlanStatus.PacketReady
			|| !packetPlan.ShouldSendPacket
			|| packetPlan.Packet == null)
		{
			return Stop(
				BindPointTeleportKinahCallbackCompositionStatus.StoppedBeforePacket,
				persistenceDecision,
				packetPlan,
				runtimeResult: null,
				"Scheduled bind-point callback has saved persistence but no inventory update packet intent, so cooldown, fanout, and movement metadata stay blocked");
		}

		if (runtimeResult == null
			|| runtimeResult.Status != BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast
			|| !runtimeResult.StoredCooldownFact
			|| !runtimeResult.BroadcastCooldown)
		{
			return Stop(
				BindPointTeleportKinahCallbackCompositionStatus.StoppedBeforeRuntimeCallback,
				persistenceDecision,
				packetPlan,
				runtimeResult,
				"Scheduled bind-point callback has packet intent but lacks stored cooldown/action 3 fanout metadata, so final movement metadata stays blocked",
				includePacket: true);
		}

		return new BindPointTeleportKinahCallbackComposition(
			BindPointTeleportKinahCallbackCompositionStatus.ReadyWithRuntimeCallback,
			persistenceDecision,
			packetPlan,
			runtimeResult,
			packetPlan.Packet,
			[
				BindPointTeleportKinahCallbackCompositionStep.CheckPersistenceDecision,
				BindPointTeleportKinahCallbackCompositionStep.CreateInventoryUpdatePacketIntent,
				BindPointTeleportKinahCallbackCompositionStep.StoreCooldown,
				BindPointTeleportKinahCallbackCompositionStep.BroadcastCooldown,
				BindPointTeleportKinahCallbackCompositionStep.ScheduleFinalTeleport,
				.. runtimeResult.ShouldTeleport
					? [BindPointTeleportKinahCallbackCompositionStep.CreateFinalMovementIntent]
					: Array.Empty<BindPointTeleportKinahCallbackCompositionStep>(),
			],
			ShouldSendInventoryUpdatePacket: true,
			ShouldStoreCooldown: true,
			ShouldBroadcastCooldown: true,
			runtimeResult.ShouldScheduleFinalTeleport,
			runtimeResult.ShouldTeleport,
			"Scheduled bind-point callback metadata is staged in Java order: saved Kinah -> SM_INVENTORY_UPDATE_ITEM intent -> addCooldown -> action 3 fanout -> final movement intent",
			IsLive: false);
	}

	private static BindPointTeleportKinahCallbackComposition Stop(
		BindPointTeleportKinahCallbackCompositionStatus status,
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		BindPointTeleportRuntimeCallbackExecutionResult? runtimeResult,
		string javaSource,
		bool includePacket = false)
	{
		return new BindPointTeleportKinahCallbackComposition(
			status,
			persistenceDecision,
			packetPlan,
			runtimeResult,
			includePacket ? packetPlan.Packet : null,
			[
				BindPointTeleportKinahCallbackCompositionStep.CheckPersistenceDecision,
				.. includePacket
					? [BindPointTeleportKinahCallbackCompositionStep.CreateInventoryUpdatePacketIntent]
					: Array.Empty<BindPointTeleportKinahCallbackCompositionStep>(),
			],
			ShouldSendInventoryUpdatePacket: includePacket && packetPlan.Packet != null,
			ShouldStoreCooldown: false,
			ShouldBroadcastCooldown: false,
			ShouldScheduleFinalTeleport: false,
			ShouldTeleport: false,
			javaSource,
			IsLive: false);
	}
}
