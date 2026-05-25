using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class NearbyQuestMarkerProjectionService
{
	public static NearbyQuestMarkerProjectionResult ProjectMarkers(
		Player player,
		WorldMapInstanceRuntimeState instance,
		NearbyQuestTemplateTable questTemplates)
	{
		// Java parity breadcrumb: PlayerController.updateNearbyQuests filters WorldMapInstance.getQuestIds().
		var markers = new List<NearbyQuestMarker>();
		var rejected = new Dictionary<int, NearbyQuestStartConditionFailure>();

		foreach (var questId in instance.QuestIds)
		{
			var result = NearbyQuestStartConditionService.CheckNearbyStartConditions(player, questId, questTemplates);
			if (!result.CanStart)
			{
				rejected[questId] = result.Failure;
				continue;
			}

			markers.Add(new NearbyQuestMarker(
				questId,
				NearbyQuestStartConditionService.GetLevelRequirementDiff(questId, player.Level, questTemplates)));
		}

		return new NearbyQuestMarkerProjectionResult(markers, rejected);
	}
}

public sealed record NearbyQuestMarkerProjectionResult(
	IReadOnlyList<NearbyQuestMarker> Markers,
	IReadOnlyDictionary<int, NearbyQuestStartConditionFailure> RejectedQuestIds);
