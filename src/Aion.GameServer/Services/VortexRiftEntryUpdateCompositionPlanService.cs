using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum VortexRiftEntryUpdateCompositionPlanStatus
{
	MissingEntryUpdate,
	MissingWorldTargets,
	MissingPlayerTargets,
	MismatchedTargetPlans,
	NoTargetPlayers,
	Ready,
}

public sealed record VortexRiftEntryUpdateCompositionPlan(
	VortexRiftEntryUpdateCompositionPlanStatus Status,
	VortexPassedPlayerSyncRiftEntryUpdateResult? EntryUpdate,
	VortexRiftEntryUpdateWorldTargetPlan? WorldTargetPlan,
	VortexRiftEntryUpdatePlayerTargetPlan? PlayerTargetPlan,
	SmRiftAnnounce? Packet,
	IReadOnlyList<int> WorldIds,
	IReadOnlyList<int> TargetPlayerObjectIds,
	bool HasPacketIntent,
	bool ReadyForDispatch,
	string JavaSource);

public static class VortexRiftEntryUpdateCompositionPlanService
{
	public static VortexRiftEntryUpdateCompositionPlan CreatePlan(
		VortexPassedPlayerSyncRiftEntryUpdateResult? entryUpdate,
		VortexRiftEntryUpdateWorldTargetPlan? worldTargetPlan,
		VortexRiftEntryUpdatePlayerTargetPlan? playerTargetPlan)
	{
		if (entryUpdate == null
			|| entryUpdate.Status != VortexPassedPlayerSyncRiftEntryUpdateStatus.Updated
			|| !entryUpdate.HasPacketIntent
			|| entryUpdate.Packet == null)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionPlanStatus.MissingEntryUpdate,
				entryUpdate,
				worldTargetPlan,
				playerTargetPlan,
				packet: null,
				[],
				[],
				readyForDispatch: false);
		}

		if (worldTargetPlan == null
			|| worldTargetPlan.Status != VortexRiftEntryUpdateWorldTargetPlanStatus.Planned
			|| worldTargetPlan.WorldIds.Count == 0)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionPlanStatus.MissingWorldTargets,
				entryUpdate,
				worldTargetPlan,
				playerTargetPlan,
				entryUpdate.Packet,
				[],
				[],
				readyForDispatch: false);
		}

		if (playerTargetPlan == null
			|| playerTargetPlan.Status != VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionPlanStatus.MissingPlayerTargets,
				entryUpdate,
				worldTargetPlan,
				playerTargetPlan,
				entryUpdate.Packet,
				worldTargetPlan.WorldIds,
				[],
				readyForDispatch: false);
		}

		if (!ReferenceEquals(playerTargetPlan.WorldTargetPlan, worldTargetPlan))
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionPlanStatus.MismatchedTargetPlans,
				entryUpdate,
				worldTargetPlan,
				playerTargetPlan,
				entryUpdate.Packet,
				worldTargetPlan.WorldIds,
				[],
				readyForDispatch: false);
		}

		if (playerTargetPlan.TargetPlayerObjectIds.Count == 0)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionPlanStatus.NoTargetPlayers,
				entryUpdate,
				worldTargetPlan,
				playerTargetPlan,
				entryUpdate.Packet,
				worldTargetPlan.WorldIds,
				[],
				readyForDispatch: false);
		}

		return CreateResult(
			VortexRiftEntryUpdateCompositionPlanStatus.Ready,
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan,
			entryUpdate.Packet,
			worldTargetPlan.WorldIds,
			playerTargetPlan.TargetPlayerObjectIds,
			readyForDispatch: true);
	}

	private static VortexRiftEntryUpdateCompositionPlan CreateResult(
		VortexRiftEntryUpdateCompositionPlanStatus status,
		VortexPassedPlayerSyncRiftEntryUpdateResult? entryUpdate,
		VortexRiftEntryUpdateWorldTargetPlan? worldTargetPlan,
		VortexRiftEntryUpdatePlayerTargetPlan? playerTargetPlan,
		SmRiftAnnounce? packet,
		IReadOnlyList<int> worldIds,
		IReadOnlyList<int> targetPlayerObjectIds,
		bool readyForDispatch)
	{
		return new VortexRiftEntryUpdateCompositionPlan(
			status,
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan,
			packet,
			worldIds,
			targetPlayerObjectIds,
			packet != null,
			readyForDispatch,
			"controllers/RVController.syncPassed(true) -> RiftInformer.sendRiftInfo(getWorldsList(this)) -> PacketSendUtility.sendPacket(player, SM_RIFT_ANNOUNCE)");
	}
}
