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
		StaticData? staticData)
	{
		// Java parity breadcrumb: WorldMapInstance.updateNearbyQuestsTask clears the pending Future,
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
				NearbyQuestRefreshInputAdapterService.CreatePlan(player, worldInstance, staticData)))
			.ToArray();

		return NearbyQuestDelayedRefreshExecutionReport.Completed(
			schedulePlan,
			clearedPendingRefresh,
			playerReports);
	}
}

public sealed record NearbyQuestDelayedRefreshExecutionReport(
	NearbyQuestDelayedRefreshExecutionStatus Status,
	WorldMapNearbyQuestRefreshSchedulePlan SchedulePlan,
	bool ClearedPendingRefresh,
	IReadOnlyList<NearbyQuestDelayedRefreshPlayerReport> PlayerReports,
	string JavaSource,
	string? MissingDependency = null)
{
	public bool WouldInvokePlayerRefresh => Status == NearbyQuestDelayedRefreshExecutionStatus.Completed;

	public static NearbyQuestDelayedRefreshExecutionReport Completed(
		WorldMapNearbyQuestRefreshSchedulePlan schedulePlan,
		bool clearedPendingRefresh,
		IReadOnlyList<NearbyQuestDelayedRefreshPlayerReport> playerReports)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.Completed,
			schedulePlan,
			clearedPendingRefresh,
			playerReports,
			"WorldMapInstance.updateNearbyQuestsTask -> forEachPlayer(PlayerController.updateNearbyQuests)");
	}

	public static NearbyQuestDelayedRefreshExecutionReport NoPlayers(
		WorldMapNearbyQuestRefreshSchedulePlan schedulePlan,
		bool clearedPendingRefresh)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.NoPlayers,
			schedulePlan,
			clearedPendingRefresh,
			Array.Empty<NearbyQuestDelayedRefreshPlayerReport>(),
			"WorldMapInstance.updateNearbyQuestsTask found no players to refresh");
	}

	public static NearbyQuestDelayedRefreshExecutionReport NotScheduled(WorldMapNearbyQuestRefreshSchedulePlan schedulePlan)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.NotScheduled,
			schedulePlan,
			ClearedPendingRefresh: false,
			Array.Empty<NearbyQuestDelayedRefreshPlayerReport>(),
			"WorldMapInstance.updateNearbyQuestsTask is not created when no schedule is planned");
	}

	public static NearbyQuestDelayedRefreshExecutionReport MissingWorldInstance(WorldMapNearbyQuestRefreshSchedulePlan schedulePlan)
	{
		return new NearbyQuestDelayedRefreshExecutionReport(
			NearbyQuestDelayedRefreshExecutionStatus.MissingWorldInstance,
			schedulePlan,
			ClearedPendingRefresh: false,
			Array.Empty<NearbyQuestDelayedRefreshPlayerReport>(),
			"WorldMapInstance.updateNearbyQuestsTask requires the world instance that owns questIds",
			"worldInstance");
	}
}

public sealed record NearbyQuestDelayedRefreshPlayerReport(
	int PlayerObjectId,
	NearbyQuestRefreshInputAdapterResult RefreshResult);

public enum NearbyQuestDelayedRefreshExecutionStatus
{
	Completed,
	NoPlayers,
	NotScheduled,
	MissingWorldInstance,
}
