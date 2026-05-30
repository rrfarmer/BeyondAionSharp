using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class NearbyQuestCandidateProjectionService
{
	public static NearbyQuestCandidateProjectionResult ProjectNpcStartQuestIds(
		WorldMapInstanceRuntimeState instance,
		QuestNpcStartTable questNpcStartTable,
		IEnumerable<int> npcTemplateIds
	)
	{
		// Java parity: WorldMapInstance.addObject(Npc) reads QuestEngine.getQuestNpc(npcId).getOnQuestStart().
		var inspectedNpcIds = new HashSet<int>();
		var matchedNpcIds = new HashSet<int>();
		var projectedQuestIds = new HashSet<int>();
		var previousQuestIds = instance.QuestIds;

		foreach (var npcTemplateId in npcTemplateIds)
		{
			inspectedNpcIds.Add(npcTemplateId);
			var registration = questNpcStartTable.GetQuestNpc(npcTemplateId);
			if (registration.OnQuestStart.Count == 0)
				continue;

			matchedNpcIds.Add(npcTemplateId);
			foreach (var questId in registration.OnQuestStart)
				projectedQuestIds.Add(questId);

			instance.RegisterQuestStartIds(registration.OnQuestStart);
		}

		var currentQuestIds = instance.QuestIds;
		var newlyRegisteredQuestIds = currentQuestIds.Except(previousQuestIds).ToHashSet();
		return new NearbyQuestCandidateProjectionResult(inspectedNpcIds, matchedNpcIds, projectedQuestIds, newlyRegisteredQuestIds, currentQuestIds);
	}
}

public sealed record NearbyQuestCandidateProjectionResult(
	IReadOnlySet<int> InspectedNpcIds,
	IReadOnlySet<int> MatchedNpcIds,
	IReadOnlySet<int> ProjectedQuestIds,
	IReadOnlySet<int> NewlyRegisteredQuestIds,
	IReadOnlySet<int> WorldQuestIds
);
