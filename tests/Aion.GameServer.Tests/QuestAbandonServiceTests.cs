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

	private static Player PlayerWithQuest(PlayerQuestState questState)
	{
		return new Player { Quests = [questState] };
	}

	private static NearbyQuestTemplateSummary Template(
		int questId = 1001,
		bool cannotGiveup = false,
		bool isTimer = false,
		int? questWorkItemId = null)
	{
		return new NearbyQuestTemplateSummary(
			questId,
			CannotGiveup: cannotGiveup,
			IsTimer: isTimer,
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
