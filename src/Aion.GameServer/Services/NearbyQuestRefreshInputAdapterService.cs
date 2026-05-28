using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class NearbyQuestRefreshInputAdapterService
{
	public static NearbyQuestRefreshInputAdapterResult CreatePlan(
		Player? player,
		WorldMapInstanceRuntimeState? worldInstance,
		StaticData? staticData)
	{
		// Java parity breadcrumb: PlayerController.updateNearbyQuests resolves the player's
		// current map-region quest ids and DataManager.QUEST_DATA before sending SM_NEARBY_QUESTS.
		if (player == null)
			return NearbyQuestRefreshInputAdapterResult.MissingPlayer();
		if (staticData == null)
			return NearbyQuestRefreshInputAdapterResult.MissingStaticData();

		return NearbyQuestRefreshInputAdapterResult.Created(
			NearbyQuestRefreshPlanService.CreatePlan(player, worldInstance, staticData.NearbyQuestTemplates));
	}
}

public sealed record NearbyQuestRefreshInputAdapterResult(
	NearbyQuestRefreshInputAdapterStatus Status,
	NearbyQuestRefreshPlan Plan,
	string JavaSource,
	string? MissingDependency = null)
{
	public bool Applied => Status == NearbyQuestRefreshInputAdapterStatus.Created;

	public static NearbyQuestRefreshInputAdapterResult MissingPlayer()
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.MissingPlayer,
			NearbyQuestRefreshPlan.NoWorldInstance(),
			"PlayerController.updateNearbyQuests requires a live player controller owner",
			"player");
	}

	public static NearbyQuestRefreshInputAdapterResult MissingStaticData()
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.MissingStaticData,
			NearbyQuestRefreshPlan.NoQuestTemplates(0),
			"PlayerController.updateNearbyQuests requires DataManager.QUEST_DATA",
			"staticData");
	}

	public static NearbyQuestRefreshInputAdapterResult Created(NearbyQuestRefreshPlan plan)
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.Created,
			plan,
			"PlayerController.updateNearbyQuests -> QuestService.checkStartConditions -> SM_NEARBY_QUESTS");
	}
}

public enum NearbyQuestRefreshInputAdapterStatus
{
	Created,
	MissingPlayer,
	MissingStaticData,
}
