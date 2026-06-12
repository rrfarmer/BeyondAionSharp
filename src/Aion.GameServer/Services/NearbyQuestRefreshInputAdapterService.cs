using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class NearbyQuestRefreshInputAdapterService
{
	public static NearbyQuestRefreshInputAdapterResult CreatePlan(Player? player, WorldMapInstanceRuntimeState? worldInstance, StaticData? staticData)
	{
		// Java parity: PlayerController.updateNearbyQuests resolves the player's
		// current map-region quest ids and DataManager.QUEST_DATA before sending SM_NEARBY_QUESTS.
		if (player == null)
			return NearbyQuestRefreshInputAdapterResult.MissingPlayer();
		if (staticData == null)
			return NearbyQuestRefreshInputAdapterResult.MissingStaticData();

		return NearbyQuestRefreshInputAdapterResult.Created(
			NearbyQuestRefreshPlanService.CreatePlan(player, worldInstance, staticData.NearbyQuestTemplates)
		);
	}

	public static NearbyQuestRefreshInputAdapterResult CreatePlanFromMapRegion(
		Player? player,
		NearbyQuestMapRegionSnapshot? mapRegion,
		StaticData? staticData
	)
	{
		// Java parity: PlayerController.updateNearbyQuests walks
		// player.getPosition().getMapRegion().getParent().getQuestIds(). This overload keeps the
		// map-region boundary explicit until live region storage and controller dispatch are ported.
		if (player == null)
			return NearbyQuestRefreshInputAdapterResult.MissingPlayer();
		if (staticData == null)
			return NearbyQuestRefreshInputAdapterResult.MissingStaticData();
		if (mapRegion == null)
			return NearbyQuestRefreshInputAdapterResult.MissingMapRegion(player.GetPosition());

		return NearbyQuestRefreshInputAdapterResult.CreatedFromMapRegion(
			NearbyQuestRefreshPlanService.CreatePlan(player, mapRegion.ParentWorldInstance, staticData.NearbyQuestTemplates),
			player.GetPosition(),
			mapRegion.Position,
			mapRegion.ParentWorldInstance?.InstanceId
		);
	}
}

public sealed record NearbyQuestMapRegionSnapshot(
	WorldPosition Position,
	WorldMapInstanceRuntimeState? ParentWorldInstance,
	string JavaSource = "Player.getPosition().getMapRegion()"
);

public sealed record NearbyQuestRefreshInputAdapterResult(
	NearbyQuestRefreshInputAdapterStatus Status,
	NearbyQuestRefreshPlan Plan,
	string JavaSource,
	string? MissingDependency = null,
	WorldPosition? PlayerPosition = null,
	WorldPosition? MapRegionPosition = null,
	int? MapRegionParentInstanceId = null
)
{
	public bool Applied => Status == NearbyQuestRefreshInputAdapterStatus.Created;

	public static NearbyQuestRefreshInputAdapterResult MissingPlayer()
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.MissingPlayer,
			NearbyQuestRefreshPlan.NoWorldInstance(),
			"PlayerController.updateNearbyQuests requires a live player controller owner",
			"player"
		);
	}

	public static NearbyQuestRefreshInputAdapterResult MissingStaticData()
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.MissingStaticData,
			NearbyQuestRefreshPlan.NoQuestTemplates(0),
			"PlayerController.updateNearbyQuests requires DataManager.QUEST_DATA",
			"staticData"
		);
	}

	public static NearbyQuestRefreshInputAdapterResult MissingMapRegion(WorldPosition playerPosition)
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.MissingMapRegion,
			NearbyQuestRefreshPlan.NoWorldInstance(),
			"PlayerController.updateNearbyQuests requires player.getPosition().getMapRegion()",
			"mapRegion",
			PlayerPosition: playerPosition
		);
	}

	public static NearbyQuestRefreshInputAdapterResult Created(NearbyQuestRefreshPlan plan)
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.Created,
			plan,
			"PlayerController.updateNearbyQuests -> QuestService.checkStartConditions -> SM_NEARBY_QUESTS"
		);
	}

	public static NearbyQuestRefreshInputAdapterResult CreatedFromMapRegion(
		NearbyQuestRefreshPlan plan,
		WorldPosition playerPosition,
		WorldPosition mapRegionPosition,
		int? mapRegionParentInstanceId
	)
	{
		return new NearbyQuestRefreshInputAdapterResult(
			NearbyQuestRefreshInputAdapterStatus.Created,
			plan,
			"PlayerController.updateNearbyQuests -> player.position.mapRegion.parent.questIds -> SM_NEARBY_QUESTS",
			PlayerPosition: playerPosition,
			MapRegionPosition: mapRegionPosition,
			MapRegionParentInstanceId: mapRegionParentInstanceId
		);
	}
}

public enum NearbyQuestRefreshInputAdapterStatus
{
	Created,
	MissingPlayer,
	MissingStaticData,
	MissingMapRegion,
}
