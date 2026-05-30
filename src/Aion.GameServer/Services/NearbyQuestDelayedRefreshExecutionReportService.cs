using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class NearbyQuestDelayedRefreshExecutionReportService
{
	public static NearbyQuestDelayedRefreshExecutionReport CreateReport(
		WorldMapNearbyQuestRefreshSchedulePlan schedulePlan,
		WorldMapInstanceRuntimeState? worldInstance,
		IReadOnlyList<Player> players,
		StaticData? staticData
	)
	{
		// Java parity: WorldMapInstance.updateNearbyQuestsTask clears the pending Future,
		// then forEachPlayer invokes PlayerController.updateNearbyQuests. This report keeps that non-live.
		if (!schedulePlan.WouldScheduleTask)
			return NearbyQuestDelayedRefreshExecutionReport.NotScheduled(schedulePlan);
		if (worldInstance == null)
			return NearbyQuestDelayedRefreshExecutionReport.MissingWorldInstance(schedulePlan);

		var clearedPendingRefresh = worldInstance.CompletePendingNearbyQuestRefresh();
		if (players.Count == 0)
			return NearbyQuestDelayedRefreshExecutionReport.NoPlayers(schedulePlan, clearedPendingRefresh);

		var playerReports = players
			.Select(player => new NearbyQuestDelayedRefreshPlayerReport(
				player.ObjectId,
				NearbyQuestRefreshInputAdapterService.CreatePlan(player, worldInstance, staticData)
			))
			.ToArray();

		return NearbyQuestDelayedRefreshExecutionReport.Completed(schedulePlan, clearedPendingRefresh, playerReports);
	}

	public static NearbyQuestDelayedRefreshExecutionReport CreateReportFromMapRegions(
		WorldMapNearbyQuestRefreshSchedulePlan schedulePlan,
		WorldMapInstanceRuntimeState? worldInstance,
		IReadOnlyList<NearbyQuestDelayedRefreshPlayerInput> players,
		StaticData? staticData
	)
	{
		// Java parity: WorldMapInstance.updateNearbyQuestsTask invokes each player's
		// PlayerController.updateNearbyQuests, which resolves quest ids from that player's current
		// position.mapRegion.parent rather than from a pre-supplied flat instance.
		if (!schedulePlan.WouldScheduleTask)
			return NearbyQuestDelayedRefreshExecutionReport.NotScheduled(schedulePlan);
		if (worldInstance == null)
			return NearbyQuestDelayedRefreshExecutionReport.MissingWorldInstance(schedulePlan);

		var clearedPendingRefresh = worldInstance.CompletePendingNearbyQuestRefresh();
		if (players.Count == 0)
			return NearbyQuestDelayedRefreshExecutionReport.NoPlayers(schedulePlan, clearedPendingRefresh);

		var playerReports = players
			.Select(input => new NearbyQuestDelayedRefreshPlayerReport(
				input.Player.ObjectId,
				NearbyQuestRefreshInputAdapterService.CreatePlanFromMapRegion(input.Player, input.MapRegion, staticData)
			))
			.ToArray();

		return NearbyQuestDelayedRefreshExecutionReport.Completed(schedulePlan, clearedPendingRefresh, playerReports);
	}

	public static NearbyQuestDelayedRefreshPacketIntentSummary CreatePacketIntentSummary(NearbyQuestDelayedRefreshExecutionReport report)
	{
		// Java parity: SM_NEARBY_QUESTS is sent even for an empty nearbyQuestList.
		var packetIntentReports = report.PlayerReports.Where(playerReport => playerReport.RefreshResult.Plan.WouldSendPacket).ToArray();
		var readyPacketCount = packetIntentReports.Count(playerReport => playerReport.RefreshResult.Plan.Status == NearbyQuestRefreshPlanStatus.Ready);
		var emptyPacketIntentCount = packetIntentReports.Count(playerReport =>
			playerReport.RefreshResult.Plan.Status is NearbyQuestRefreshPlanStatus.NoWorldQuestIds or NearbyQuestRefreshPlanStatus.NoMarkers
		);
		var rejectionCounts = report
			.PlayerReports.SelectMany(playerReport => playerReport.RefreshResult.Plan.RejectionCounts)
			.GroupBy(pair => pair.Key)
			.ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));
		var unsupportedDependencyCount = SumUnsupportedDependencyCount(rejectionCounts);
		return new NearbyQuestDelayedRefreshPacketIntentSummary(
			report.PlayerReports.Count,
			packetIntentReports.Length,
			readyPacketCount,
			emptyPacketIntentCount,
			rejectionCounts,
			unsupportedDependencyCount
		);
	}

	private static int SumUnsupportedDependencyCount(IReadOnlyDictionary<NearbyQuestStartConditionFailure, int> rejectionCounts)
	{
		var total = 0;
		if (rejectionCounts.TryGetValue(NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions, out var xmlConditions))
			total += xmlConditions;
		if (rejectionCounts.TryGetValue(NearbyQuestStartConditionFailure.UnsupportedInventoryItems, out var inventoryItems))
			total += inventoryItems;
		if (rejectionCounts.TryGetValue(NearbyQuestStartConditionFailure.UnsupportedRepeatTiming, out var repeatTiming))
			total += repeatTiming;
		return total;
	}
}

public sealed record NearbyQuestDelayedRefreshExecutionReport(
	NearbyQuestDelayedRefreshExecutionStatus Status,
	WorldMapNearbyQuestRefreshSchedulePlan SchedulePlan,
	bool ClearedPendingRefresh,
	IReadOnlyList<NearbyQuestDelayedRefreshPlayerReport> PlayerReports,
	string JavaSource,
	string? MissingDependency = null
)
{
	public bool WouldInvokePlayerRefresh => Status == NearbyQuestDelayedRefreshExecutionStatus.Completed;

	public static NearbyQuestDelayedRefreshExecutionReport Completed(
		WorldMapNearbyQuestRefreshSchedulePlan schedulePlan,
		bool clearedPendingRefresh,
		IReadOnlyList<NearbyQuestDelayedRefreshPlayerReport> playerReports
	)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.Completed,
			schedulePlan,
			clearedPendingRefresh,
			playerReports,
			"WorldMapInstance.updateNearbyQuestsTask -> forEachPlayer(PlayerController.updateNearbyQuests)"
		);
	}

	public static NearbyQuestDelayedRefreshExecutionReport NoPlayers(WorldMapNearbyQuestRefreshSchedulePlan schedulePlan, bool clearedPendingRefresh)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.NoPlayers,
			schedulePlan,
			clearedPendingRefresh,
			Array.Empty<NearbyQuestDelayedRefreshPlayerReport>(),
			"WorldMapInstance.updateNearbyQuestsTask found no players to refresh"
		);
	}

	public static NearbyQuestDelayedRefreshExecutionReport NotScheduled(WorldMapNearbyQuestRefreshSchedulePlan schedulePlan)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.NotScheduled,
			schedulePlan,
			ClearedPendingRefresh: false,
			Array.Empty<NearbyQuestDelayedRefreshPlayerReport>(),
			"WorldMapInstance.updateNearbyQuestsTask is not created when no schedule is planned"
		);
	}

	public static NearbyQuestDelayedRefreshExecutionReport MissingWorldInstance(WorldMapNearbyQuestRefreshSchedulePlan schedulePlan)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.MissingWorldInstance,
			schedulePlan,
			ClearedPendingRefresh: false,
			Array.Empty<NearbyQuestDelayedRefreshPlayerReport>(),
			"WorldMapInstance.updateNearbyQuestsTask requires the world instance that owns questIds",
			"worldInstance"
		);
	}
}

public sealed record NearbyQuestDelayedRefreshPlayerReport(int PlayerObjectId, NearbyQuestRefreshInputAdapterResult RefreshResult);

public sealed record NearbyQuestDelayedRefreshPlayerInput(Player Player, NearbyQuestMapRegionSnapshot? MapRegion);

public sealed record NearbyQuestDelayedRefreshPacketIntentSummary(
	int PlayerCount,
	int PacketIntentCount,
	int ReadyPacketCount,
	int EmptyPacketIntentCount,
	IReadOnlyDictionary<NearbyQuestStartConditionFailure, int> RejectionCounts,
	int UnsupportedDependencyCount
)
{
	public bool HasPacketIntent => PacketIntentCount > 0;
}

public enum NearbyQuestDelayedRefreshExecutionStatus
{
	Completed,
	NoPlayers,
	NotScheduled,
	MissingWorldInstance,
}
