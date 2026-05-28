using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
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
		Assert.True(plan.WouldSendPacket);
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
		Assert.True(plan.WouldSendPacket);
		Assert.Empty(plan.Markers);
		Assert.Equal(2, plan.RejectedQuestIds.Count);
		Assert.Equal(1, plan.RejectionCounts[NearbyQuestStartConditionFailure.Race]);
		Assert.Equal(1, plan.RejectionCounts[NearbyQuestStartConditionFailure.MinLevel]);
		Assert.False(plan.HasUnsupportedDependencies);
	}

	[Fact]
	public void CreatePlan_MatchesJavaEmptyNearbyQuestPacketIntent()
	{
		var player = CreatePlayer();
		var emptyInstance = new WorldMapInstanceRuntimeState(instanceId: 1);
		var rejectedInstance = new WorldMapInstanceRuntimeState(instanceId: 2);
		rejectedInstance.RegisterQuestStartIds([3001]);
		var templates = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(3001, RacePermitted: "ASMODIANS"),
		]);

		var emptyPlan = NearbyQuestRefreshPlanService.CreatePlan(player, emptyInstance, templates);
		var rejectedPlan = NearbyQuestRefreshPlanService.CreatePlan(player, rejectedInstance, templates);

		Assert.True(emptyPlan.WouldSendPacket);
		Assert.Equal(NearbyQuestRefreshPlanStatus.NoWorldQuestIds, emptyPlan.Status);
		Assert.Empty(emptyPlan.Markers);
		Assert.True(rejectedPlan.WouldSendPacket);
		Assert.Equal(NearbyQuestRefreshPlanStatus.NoMarkers, rejectedPlan.Status);
		Assert.Empty(rejectedPlan.Markers);
		Assert.Equal(NearbyQuestStartConditionFailure.Race, rejectedPlan.RejectedQuestIds[3001]);
	}

	[Fact]
	public void CreatePacketFactoryPlan_CreatesSmNearbyQuestsForReadyPlan()
	{
		var player = CreatePlayer();
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);
		instance.RegisterQuestStartIds([1001]);
		var templates = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(1001, MinLevelPermitted: 22),
		]);
		var refreshPlan = NearbyQuestRefreshPlanService.CreatePlan(player, instance, templates);

		var packetPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(refreshPlan);

		Assert.Equal(NearbyQuestPacketFactoryPlanStatus.PacketCreated, packetPlan.Status);
		Assert.True(packetPlan.HasPacket);
		Assert.Contains("SM_NEARBY_QUESTS", packetPlan.JavaSource);
		var marker = Assert.Single(packetPlan.Markers);
		Assert.Equal(1001, marker.QuestId);
		Assert.Equal(2, marker.LevelRequirementDiff);
		Assert.Equal(
			Convert.FromHexString("00FFFFE9030200"),
			SerializeUnencryptedPayload(packetPlan.Packet!));
	}

	[Fact]
	public void CreatePacketFactoryPlan_CreatesEmptySmNearbyQuestsWhenJavaWouldSendEmptyMap()
	{
		var emptyWorldPlan = NearbyQuestRefreshPlan.NoWorldQuestIds();
		var noMarkersPlan = new NearbyQuestRefreshPlan(
			NearbyQuestRefreshPlanStatus.NoMarkers,
			WorldQuestIdCount: 1,
			Markers: [],
			RejectedQuestIds: new Dictionary<int, NearbyQuestStartConditionFailure>
			{
				[3001] = NearbyQuestStartConditionFailure.Race,
			},
			RejectionCounts: new Dictionary<NearbyQuestStartConditionFailure, int>
			{
				[NearbyQuestStartConditionFailure.Race] = 1,
			});

		var emptyWorldPacketPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(emptyWorldPlan);
		var noMarkersPacketPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(noMarkersPlan);

		Assert.True(emptyWorldPacketPlan.HasPacket);
		Assert.True(noMarkersPacketPlan.HasPacket);
		Assert.Equal(Convert.FromHexString("000000"), SerializeUnencryptedPayload(emptyWorldPacketPlan.Packet!));
		Assert.Equal(Convert.FromHexString("000000"), SerializeUnencryptedPayload(noMarkersPacketPlan.Packet!));
	}

	[Fact]
	public void CreatePacketFactoryPlan_BlocksWhenRefreshPrerequisitesAreMissing()
	{
		var noWorldInstancePlan = NearbyQuestRefreshPlan.NoWorldInstance();
		var noQuestTemplatesPlan = NearbyQuestRefreshPlan.NoQuestTemplates(worldQuestIdCount: 1);

		var noWorldPacketPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(noWorldInstancePlan);
		var noTemplatesPacketPlan = NearbyQuestRefreshPlanService.CreatePacketFactoryPlan(noQuestTemplatesPlan);

		Assert.Equal(NearbyQuestPacketFactoryPlanStatus.BlockedMissingDependency, noWorldPacketPlan.Status);
		Assert.Equal(NearbyQuestPacketFactoryPlanStatus.BlockedMissingDependency, noTemplatesPacketPlan.Status);
		Assert.False(noWorldPacketPlan.HasPacket);
		Assert.False(noTemplatesPacketPlan.HasPacket);
		Assert.Contains(nameof(NearbyQuestRefreshPlanStatus.NoWorldInstance), noWorldPacketPlan.JavaSource);
		Assert.Contains(nameof(NearbyQuestRefreshPlanStatus.NoQuestTemplates), noTemplatesPacketPlan.JavaSource);
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

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
