using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestRefreshPlanServiceTests
{
	[Fact]
	public void CreatePlan_FailsClosedWithoutWorldInstanceOrQuestTemplates()
	{
		var player = CreatePlayer();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIds([1001]);

		var noInstance = NearbyQuestRefreshPlanService.CreatePlan(player, instance: null, questTemplates: null);
		var noTemplates = NearbyQuestRefreshPlanService.CreatePlan(player, instance, questTemplates: null);

		Assert.Equal(NearbyQuestRefreshPlanStatus.NoWorldInstance, noInstance.Status);
		Assert.False(noInstance.WouldSendPacket);
		Assert.Empty(noInstance.Markers);
		Assert.Empty(noInstance.RejectedQuestIds);
		Assert.Equal(NearbyQuestRefreshPlanStatus.NoQuestTemplates, noTemplates.Status);
		Assert.Equal(1, noTemplates.WorldQuestIdCount);
		Assert.False(noTemplates.WouldSendPacket);
	}

	[Fact]
	public void CreatePlan_ReturnsNoWorldQuestIdsWithoutSending()
	{
		var plan = NearbyQuestRefreshPlanService.CreatePlan(
			CreatePlayer(),
			new WorldMapInstanceRuntimeState(instanceId: 1),
			new NearbyQuestTemplateTable([]));

		Assert.Equal(NearbyQuestRefreshPlanStatus.NoWorldQuestIds, plan.Status);
		Assert.False(plan.WouldSendPacket);
		Assert.Empty(plan.Markers);
		Assert.Empty(plan.RejectedQuestIds);
		Assert.Empty(plan.RejectionCounts);
	}

	[Fact]
	public void CreatePlan_ComposesMarkersAndRejectionReasonsWithoutSending()
	{
		var player = CreatePlayer();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIds([1001, 1002, 1003, 1004]);
		var templates = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(1001, MinLevelPermitted: 22),
			new NearbyQuestTemplateSummary(1002, RacePermitted: "ASMODIANS"),
			new NearbyQuestTemplateSummary(1003, HasXmlStartConditions: true),
		]);

		var plan = NearbyQuestRefreshPlanService.CreatePlan(player, instance, templates);

		Assert.Equal(NearbyQuestRefreshPlanStatus.Ready, plan.Status);
		Assert.True(plan.WouldSendPacket);
		Assert.Equal(4, plan.WorldQuestIdCount);
		var marker = Assert.Single(plan.Markers);
		Assert.Equal(1001, marker.QuestId);
		Assert.Equal(2, marker.LevelRequirementDiff);
		Assert.Equal(
			NearbyQuestStartConditionFailure.Race,
			plan.RejectedQuestIds[1002]);
		Assert.Equal(
			NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions,
			plan.RejectedQuestIds[1003]);
		Assert.Equal(
			NearbyQuestStartConditionFailure.MissingTemplate,
			plan.RejectedQuestIds[1004]);
		Assert.Equal(1, plan.RejectionCounts[NearbyQuestStartConditionFailure.Race]);
		Assert.Equal(1, plan.RejectionCounts[NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions]);
		Assert.True(plan.HasUnsupportedDependencies);
	}

	[Fact]
	public void CreatePlan_ReturnsNoMarkersWhenAllQuestIdsAreRejected()
	{
		var player = CreatePlayer();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIds([2001, 2002]);
		var templates = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(2001, RacePermitted: "ASMODIANS"),
			new NearbyQuestTemplateSummary(2002, MinLevelPermitted: 23),
		]);

		var plan = NearbyQuestRefreshPlanService.CreatePlan(player, instance, templates);

		Assert.Equal(NearbyQuestRefreshPlanStatus.NoMarkers, plan.Status);
		Assert.False(plan.WouldSendPacket);
		Assert.Empty(plan.Markers);
		Assert.Equal(2, plan.RejectedQuestIds.Count);
		Assert.Equal(1, plan.RejectionCounts[NearbyQuestStartConditionFailure.Race]);
		Assert.Equal(1, plan.RejectionCounts[NearbyQuestStartConditionFailure.MinLevel]);
		Assert.False(plan.HasUnsupportedDependencies);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			Level = 20,
			Race = "ELYOS",
			PlayerClass = "GLADIATOR",
			Gender = "MALE",
		};
	}
}
