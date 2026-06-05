using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestAbandonServiceTests
{
	[Fact]
	public void Abandon_FirstTimeStartedQuestDeletesStateAndSendsAbandonPacket()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1001, "START", QuestVars: 0x22, Flags: 2, CompleteCount: 0));

		var result = QuestAbandonService.Abandon(player, 1001, Template());

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.True(result.Mutated);
		Assert.Empty(player.Quests);
		Assert.True(result.NearbyQuestRefreshRequired);
		Assert.Empty(result.TimerPackets);
		Assert.Empty(result.WorkItemDeletions);
		Assert.NotNull(result.AbandonPacket);
		Assert.Equal(Convert.FromHexString("03E903000000000000"), SerializeUnencryptedPayload(result.AbandonPacket!));
	}

	[Fact]
	public void Abandon_PreviouslyCompletedQuestResetsToCompleteLikeJava()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1002, "START", QuestVars: 0x1122, Flags: 3, CompleteCount: 2));

		var result = QuestAbandonService.Abandon(player, 1002, Template(1002));

		Assert.Equal(QuestAbandonStatus.ResetToComplete, result.Status);
		var reset = Assert.Single(player.Quests);
		Assert.Equal("COMPLETE", reset.Status);
		Assert.Equal(0, reset.QuestVars);
		Assert.Equal(0, reset.Flags);
		Assert.Equal(2, reset.CompleteCount);
		Assert.Same(reset, result.FinalQuestState);
		Assert.True(result.NearbyQuestRefreshRequired);
		Assert.NotNull(result.AbandonPacket);
		Assert.Equal(Convert.FromHexString("03EA03000000000000"), SerializeUnencryptedPayload(result.AbandonPacket!));
	}

	[Theory]
	[InlineData("COMPLETE", QuestAbandonStatus.AlreadyComplete)]
	[InlineData("LOCKED", QuestAbandonStatus.Locked)]
	public void Abandon_CompleteAndLockedStatesDoNotMutate(string status, QuestAbandonStatus expected)
	{
		var original = new PlayerQuestState(1003, status, QuestVars: 7, Flags: 1, CompleteCount: 1);
		var player = PlayerWithQuest(original);

		var result = QuestAbandonService.Abandon(player, 1003, Template(1003));

		Assert.Equal(expected, result.Status);
		Assert.False(result.Mutated);
		Assert.Same(original, Assert.Single(player.Quests));
		Assert.Empty(result.TimerPackets);
		Assert.Empty(result.WorkItemDeletions);
		Assert.Null(result.AbandonPacket);
		Assert.False(result.NearbyQuestRefreshRequired);
	}

	[Fact]
	public void Abandon_CannotGiveupTemplateBlocksMutation()
	{
		var questState = new PlayerQuestState(1004, "START", QuestVars: 1, Flags: 0, CompleteCount: 0);
		var player = PlayerWithQuest(questState);

		var result = QuestAbandonService.Abandon(player, 1004, Template(1004, cannotGiveup: true));

		Assert.Equal(QuestAbandonStatus.CannotGiveup, result.Status);
		Assert.False(result.Mutated);
		Assert.Same(questState, Assert.Single(player.Quests));
		Assert.Empty(result.TimerPackets);
		Assert.Empty(result.WorkItemDeletions);
		Assert.Null(result.AbandonPacket);
	}

	[Fact]
	public void Abandon_TimerTemplateSendsTimerClearBeforeAbandon()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1005, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));

		var result = QuestAbandonService.Abandon(player, 1005, Template(1005, isTimer: true));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.Equal(Convert.FromHexString("04ED0300000000000000"), SerializeUnencryptedPayload(Assert.Single(result.TimerPackets)));
		Assert.NotNull(result.AbandonPacket);
		Assert.Equal(Convert.FromHexString("03ED03000000000000"), SerializeUnencryptedPayload(result.AbandonPacket!));
	}

	[Fact]
	public void Abandon_MissingTemplateReturnsFalseLikeJava()
	{
		var questState = new PlayerQuestState(1006, "START", QuestVars: 0, Flags: 0, CompleteCount: 0);
		var player = PlayerWithQuest(questState);

		var result = QuestAbandonService.Abandon(player, 1006, template: null);

		Assert.Equal(QuestAbandonStatus.MissingTemplate, result.Status);
		Assert.False(result.Mutated);
		Assert.Same(questState, Assert.Single(player.Quests));
		Assert.Empty(result.TimerPackets);
		Assert.Empty(result.WorkItemDeletions);
		Assert.Null(result.AbandonPacket);
	}

	[Fact]
	public void Abandon_RemovesAllMatchingQuestWorkItemStacksFromCube()
	{
		var kept = Item(5001, 188000002, 3);
		var firstWorkItem = Item(5002, 188000001, 2);
		var secondWorkItem = Item(5003, 188000001, 5);
		var equippedWorkItem = Item(5004, 188000001, 1, isEquipped: true);
		var player = PlayerWithQuest(new PlayerQuestState(1007, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));
		player.InventoryItems = [kept, firstWorkItem, secondWorkItem, equippedWorkItem];

		var result = QuestAbandonService.Abandon(player, 1007, Template(1007, questWorkItemId: 188000001));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.Equal([5002, 5003], result.WorkItemDeletions.Select(deletion => deletion.Item.ObjectId).Order());
		Assert.All(result.WorkItemDeletions, deletion => Assert.Equal(SmDeleteItem.QuestStartDeleteType, deletion.DeleteType));
		Assert.Equal([2, 1], result.WorkItemDeletions.Select(deletion => deletion.CubeItemCountAfterDeletion));
		Assert.Equal([5001, 5004], player.InventoryItems.Select(item => item.ObjectId).Order());
		Assert.Equal([5002, 5003], player.DeletedInventoryItems.Select(item => item.ObjectId).Order());
		Assert.Equal(StoragePersistentState.UpdateRequired, player.InventoryStoragePersistentState);
	}

	[Fact]
	public void Abandon_PreviouslyCompletedQuestWorkItemsUseQuestCompleteDeleteType()
	{
		var workItem = Item(6001, 188000003, 1);
		var player = PlayerWithQuest(new PlayerQuestState(1008, "START", QuestVars: 9, Flags: 2, CompleteCount: 1));
		player.InventoryItems = [workItem];

		var result = QuestAbandonService.Abandon(player, 1008, Template(1008, questWorkItemId: 188000003));

		Assert.Equal(QuestAbandonStatus.ResetToComplete, result.Status);
		var deletion = Assert.Single(result.WorkItemDeletions);
		Assert.Equal(6001, deletion.Item.ObjectId);
		Assert.Equal(SmDeleteItem.QuestCompleteDeleteType, deletion.DeleteType);
		Assert.Equal(0, deletion.CubeItemCountAfterDeletion);
		Assert.Empty(player.InventoryItems);
	}

	[Fact]
	public void Abandon_NpcFactionQuestResetsActiveFactionStateLikeJava()
	{
		var activeFaction = new PlayerNpcFactionState(
			FactionId: 42,
			IsActive: true,
			IsMentor: false,
			TimeEpochSeconds: 123456,
			State: PlayerNpcFactionQuestState.Start,
			QuestId: 1009);
		var mentorFaction = new PlayerNpcFactionState(
			FactionId: 77,
			IsActive: true,
			IsMentor: true,
			TimeEpochSeconds: 654321,
			State: PlayerNpcFactionQuestState.Start,
			QuestId: 2009);
		var player = PlayerWithQuest(new PlayerQuestState(1009, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));
		player.NpcFactions = new PlayerNpcFactionsSnapshot([activeFaction, mentorFaction]);

		var result = QuestAbandonService.Abandon(player, 1009, Template(1009, npcFactionId: 42));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.NotNull(result.NpcFactionAbort);
		Assert.True(result.NpcFactionAbort.Applied);
		Assert.Equal(PlayerNpcFactionQuestState.Start, result.NpcFactionAbort.PreviousFaction?.State);
		Assert.True(player.NpcFactions.TryGetFaction(42, out var abortedFaction));
		Assert.NotNull(abortedFaction);
		Assert.True(abortedFaction.IsActive);
		Assert.Equal(1009, abortedFaction.QuestId);
		Assert.Equal(123456, abortedFaction.TimeEpochSeconds);
		Assert.Equal(PlayerNpcFactionQuestState.Noting, abortedFaction.State);
		Assert.Equal(77, player.NpcFactions.GetActiveFaction(isMentor: true)?.FactionId);
	}

	[Fact]
	public void Abandon_NpcFactionQuestLeavesInactiveFactionUnchangedLikeJava()
	{
		var inactiveFaction = new PlayerNpcFactionState(
			FactionId: 43,
			IsActive: false,
			IsMentor: false,
			TimeEpochSeconds: 111,
			State: PlayerNpcFactionQuestState.Start,
			QuestId: 1010);
		var player = PlayerWithQuest(new PlayerQuestState(1010, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));
		player.NpcFactions = new PlayerNpcFactionsSnapshot([inactiveFaction]);

		var result = QuestAbandonService.Abandon(player, 1010, Template(1010, npcFactionId: 43));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.NotNull(result.NpcFactionAbort);
		Assert.Equal(PlayerNpcFactionAbortStatus.InactiveFaction, result.NpcFactionAbort.Status);
		Assert.True(player.NpcFactions.TryGetFaction(43, out var unchangedFaction));
		Assert.NotNull(unchangedFaction);
		Assert.False(unchangedFaction.IsActive);
		Assert.Equal(PlayerNpcFactionQuestState.Start, unchangedFaction.State);
	}

	[Fact]
	public void Abandon_TaskWorkOrderQuestReturnsRecipeDeleteCandidateLikeJava()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1011, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));
		player.Recipes = [155004001, 155004099];

		var result = QuestAbandonService.Abandon(
			player,
			1011,
			Template(1011, questCategory: "TASK", workOrderRecipeId: 155004001));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.Equal(155004001, result.WorkOrderRecipeId);
	}

	[Theory]
	[InlineData("QUEST", 155004001)]
	[InlineData("TASK", 0)]
	public void Abandon_NonWorkOrderTemplatesDoNotReturnRecipeDeleteCandidate(string questCategory, int workOrderRecipeId)
	{
		var player = PlayerWithQuest(new PlayerQuestState(1012, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));

		var result = QuestAbandonService.Abandon(
			player,
			1012,
			Template(1012, questCategory: questCategory, workOrderRecipeId: workOrderRecipeId));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.Null(result.WorkOrderRecipeId);
	}

	private static Player PlayerWithQuest(PlayerQuestState questState)
	{
		return new Player { Quests = [questState] };
	}

	private static NearbyQuestTemplateSummary Template(
		int questId = 1001,
		bool cannotGiveup = false,
		bool isTimer = false,
		int? questWorkItemId = null,
		int npcFactionId = 0,
		string questCategory = "QUEST",
		int workOrderRecipeId = 0)
	{
		return new NearbyQuestTemplateSummary(
			questId,
			CannotGiveup: cannotGiveup,
			IsTimer: isTimer,
			QuestCategory: questCategory,
			WorkOrderRecipeId: workOrderRecipeId,
			NpcFactionId: npcFactionId,
			QuestWorkItems: questWorkItemId == null ? null : [new NearbyQuestInventoryItem(questWorkItemId.Value)]);
	}

	private static InventoryItem Item(int objectId, int itemId, long count, bool isEquipped = false)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			IsEquipped = isEquipped,
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
