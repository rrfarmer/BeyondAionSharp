using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcQuestDropServiceTests
{
	[Fact]
	public void CreateDrops_SoloRequiresStartedQuestStepAndMissingCollectItem()
	{
		var service = new WorldNpcQuestDropService(
			new QuestDropTable(
			[
				CreateDrop(questId: 1001, npcId: 210671, itemId: 182200001, collectingStep: 7, collectCount: 3),
			]),
			chanceRoll: () => 0f);
		var npc = CreateNpc(5001, 210671);
		var looter = new Player
		{
			ObjectId = 1001,
			Quests = [new PlayerQuestState(1001, "START", QuestVars(var0: 7), Flags: 0, CompleteCount: 0)],
			InventoryItems = [new InventoryItem { ItemId = 182200001, Count = 2 }],
		};

		var result = service.CreateDrops(npc, looter, startIndex: 4);

		var drop = Assert.Single(result.Drops);
		Assert.Equal(4, drop.Index);
		Assert.Equal(182200001, drop.ItemId);
		Assert.Equal(1, drop.Count);
		Assert.Equal(5001, drop.NpcObjectId);
		Assert.True(drop.CanViewDropItem(1001));
		Assert.False(drop.CanViewDropItem(1002));
		Assert.Equal([1001], result.AllowedLooterObjectIds);
		Assert.Equal(5, result.NextIndex);
	}

	[Fact]
	public void CreateDrops_SkipsWhenCollectItemAlreadySatisfied()
	{
		var service = new WorldNpcQuestDropService(
			new QuestDropTable(
			[
				CreateDrop(questId: 1001, npcId: 210671, itemId: 182200001, collectingStep: 7, collectCount: 3),
			]),
			chanceRoll: () => 0f);
		var looter = new Player
		{
			ObjectId = 1001,
			Quests = [new PlayerQuestState(1001, "START", QuestVars(var0: 7), Flags: 0, CompleteCount: 0)],
			InventoryItems = [new InventoryItem { ItemId = 182200001, Count = 3 }],
		};

		var result = service.CreateDrops(CreateNpc(5001, 210671), looter);

		Assert.Empty(result.Drops);
		Assert.Empty(result.AllowedLooterObjectIds);
		Assert.Equal(1, result.NextIndex);
	}

	[Fact]
	public void CreateDrops_GroupEachMemberCreatesPlayerScopedDrops()
	{
		var service = new WorldNpcQuestDropService(
			new QuestDropTable(
			[
				CreateDrop(
					questId: 2001,
					npcId: 210672,
					itemId: 182200002,
					dropEachMember: 1,
					collectCount: 2),
			]),
			chanceRoll: () => 0f);
		var npc = CreateNpc(5001, 210672);
		var looter = new Player
		{
			ObjectId = 1001,
			TeamMembership = PlayerTeamMembership.Group,
			Quests = [new PlayerQuestState(2001, "START", QuestVars(), Flags: 0, CompleteCount: 0)],
			InventoryItems = [new InventoryItem { ItemId = 182200002, Count = 1 }],
		};
		var member = new Player
		{
			ObjectId = 1002,
			TeamMembership = PlayerTeamMembership.Group,
			Quests = [new PlayerQuestState(2001, "START", QuestVars(), Flags: 0, CompleteCount: 0)],
		};
		var completedMember = new Player
		{
			ObjectId = 1003,
			TeamMembership = PlayerTeamMembership.Group,
			Quests = [new PlayerQuestState(2001, "COMPLETE", QuestVars(), Flags: 0, CompleteCount: 1)],
		};

		var result = service.CreateDrops(npc, looter, [looter, member, completedMember], startIndex: 2);

		Assert.Equal([1001, 1002], result.AllowedLooterObjectIds.OrderBy(id => id).ToArray());
		Assert.Equal([2, 3], result.Drops.Select(drop => drop.Index).ToArray());
		Assert.All(result.Drops, drop => Assert.Equal(182200002, drop.ItemId));
		Assert.True(result.Drops[0].CanViewDropItem(1001));
		Assert.False(result.Drops[0].CanViewDropItem(1002));
		Assert.True(result.Drops[1].CanViewDropItem(1002));
		Assert.False(result.Drops[1].CanViewDropItem(1001));
		Assert.Equal(4, result.NextIndex);
	}

	[Fact]
	public void CreateDrops_SharedAllianceDropAllowsEveryEligibleMember()
	{
		var service = new WorldNpcQuestDropService(
			new QuestDropTable(
			[
				CreateDrop(
					questId: 3001,
					npcId: 210673,
					itemId: 182200003,
					target: "ALLIANCE"),
			]),
			chanceRoll: () => 0f);
		var looter = new Player
		{
			ObjectId = 1001,
			TeamMembership = PlayerTeamMembership.Alliance,
			Quests = [new PlayerQuestState(3001, "START", QuestVars(), Flags: 0, CompleteCount: 0)],
		};
		var member = new Player
		{
			ObjectId = 1002,
			TeamMembership = PlayerTeamMembership.Alliance,
			Quests = [new PlayerQuestState(3001, "START", QuestVars(), Flags: 0, CompleteCount: 0)],
		};

		var result = service.CreateDrops(CreateNpc(5001, 210673), looter, [looter, member]);

		var drop = Assert.Single(result.Drops);
		Assert.True(drop.CanViewDropItem(1001));
		Assert.True(drop.CanViewDropItem(1002));
		Assert.False(drop.CanViewDropItem(1003));
		Assert.Equal([1001, 1002], result.AllowedLooterObjectIds.OrderBy(id => id).ToArray());
	}

	[Fact]
	public void CreateDrops_SkipsWhenChanceFails()
	{
		var service = new WorldNpcQuestDropService(
			new QuestDropTable([CreateDrop(questId: 1001, npcId: 210671, itemId: 182200001, chance: 50)]),
			chanceRoll: () => 50f);
		var looter = new Player
		{
			ObjectId = 1001,
			Quests = [new PlayerQuestState(1001, "START", QuestVars(), Flags: 0, CompleteCount: 0)],
		};

		var result = service.CreateDrops(CreateNpc(5001, 210671), looter);

		Assert.Empty(result.Drops);
		Assert.Empty(result.AllowedLooterObjectIds);
	}

	private static QuestDropSummary CreateDrop(
		int questId,
		int npcId,
		int itemId,
		int chance = 100,
		int dropEachMember = 0,
		int collectingStep = 0,
		int collectCount = 0,
		string target = "NONE",
		string mentorType = "NONE")
	{
		return new QuestDropSummary(
			questId,
			npcId,
			itemId,
			chance,
			dropEachMember,
			collectingStep,
			target,
			mentorType,
			collectCount > 0 ? [new QuestCollectItemSummary(itemId, collectCount)] : []);
	}

	private static WorldNpc CreateNpc(int objectId, int templateId)
	{
		return new WorldNpc(
			objectId,
			templateId,
			new NpcTemplateSummary(templateId, "quest_npc", 0, 10, "NORMAL", "NORMAL", "NONE", "NONE", "GENERAL"),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static int QuestVars(int var0 = 0)
	{
		return var0;
	}
}
