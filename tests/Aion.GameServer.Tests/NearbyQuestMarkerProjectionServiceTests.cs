using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestMarkerProjectionServiceTests
{
	[Fact]
	public void ProjectMarkers_FiltersWorldQuestIdsThroughStagedNearbyPredicateWithoutSendingPacket()
	{
		var player = new Player
		{
			Level = 20,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
			Quests = [new PlayerQuestState(1005, "START", QuestVars: 0, Flags: 0, CompleteCount: 0)],
		};
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIds([1001, 1002, 1003, 1004, 1005, 1999]);
		var templates = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(1001, MinLevelPermitted: 22, RacePermitted: "PC_ALL"),
			new NearbyQuestTemplateSummary(1002, MinLevelPermitted: 23),
			new NearbyQuestTemplateSummary(1003, RacePermitted: "ASMODIANS"),
			new NearbyQuestTemplateSummary(1004, HasXmlStartConditions: true),
			new NearbyQuestTemplateSummary(1005),
		]);

		var result = NearbyQuestMarkerProjectionService.ProjectMarkers(player, instance, templates);

		var marker = Assert.Single(result.Markers);
		Assert.Equal(1001, marker.QuestId);
		Assert.Equal(2, marker.LevelRequirementDiff);
		Assert.Equal(5, result.RejectedQuestIds.Count);
		Assert.Equal(NearbyQuestStartConditionFailure.MinLevel, result.RejectedQuestIds[1002]);
		Assert.Equal(NearbyQuestStartConditionFailure.Race, result.RejectedQuestIds[1003]);
		Assert.Equal(NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions, result.RejectedQuestIds[1004]);
		Assert.Equal(NearbyQuestStartConditionFailure.AlreadyStarted, result.RejectedQuestIds[1005]);
		Assert.Equal(NearbyQuestStartConditionFailure.MissingTemplate, result.RejectedQuestIds[1999]);
	}

	[Fact]
	public void ProjectMarkers_PreservesPositiveAndNegativeLevelDiffsForPacketMarkerRule()
	{
		var player = new Player
		{
			Level = 20,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
		};
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIds([2001, 2002]);
		var templates = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(2001, MinLevelPermitted: 22),
			new NearbyQuestTemplateSummary(2002, MinLevelPermitted: 18),
		]);

		var result = NearbyQuestMarkerProjectionService.ProjectMarkers(player, instance, templates);

		Assert.Equal([2001, 2002], result.Markers.Select(marker => marker.QuestId).Order());
		Assert.Equal(2, result.Markers.Single(marker => marker.QuestId == 2001).LevelRequirementDiff);
		Assert.Equal(-2, result.Markers.Single(marker => marker.QuestId == 2002).LevelRequirementDiff);
		Assert.Empty(result.RejectedQuestIds);
	}
}
