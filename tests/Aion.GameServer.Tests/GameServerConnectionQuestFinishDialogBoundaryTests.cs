using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionQuestFinishDialogBoundaryTests
{
	private const int SelectedQuestAutoReward = 108;
	private const int SelectedQuestAutoReward1 = 110;
	private const int KinahItemId = 182400001;
	private const int RewardItemId = 186000001;
	private const int SelectableRewardItemId = 186000002;
	private const int ClassSelectableRewardItemId = 186000003;
	private const int ExtendedSelectableRewardItemId = 186000004;
	private const int WorkItemId = 182200003;
	private const int QuestReportNpcObjectId = 7001;
	private const int QuestReportNpcTemplateId = 203001;
	private const int RewardTitleId = 5;
	private const int RegularWarehouse = 1;
	private const int AccountWarehouse = 2;

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesXpAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1001, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1001, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "QuestFinishBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1001,
			extendedRewardIndex: 0);

		var stagedPlan = QuestFinishSocketGuardedOperationCompositionPlanService.CreatePlan(
			player,
			packet,
			fixture.StaticData.QuestFinishRewardProjections,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 11, 0, 0, TimeSpan.Zero),
			new GameServerOptions());

		Assert.Equal(QuestFinishSocketGuardedOperationCompositionStatus.Composed, stagedPlan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, stagedPlan.GuardedInputPlan.GuardPlan.Status);
		Assert.Equal(QuestFinishSocketInputAssemblyStatus.Ready, stagedPlan.GuardedInputPlan.InputPlan?.Status);
		Assert.NotNull(stagedPlan.OperationCompositionPlan?.OperationPlan);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(300, player.Exp);
		Assert.Equal(1, player.Level);
		Assert.Empty(player.InventoryItems);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_NpcTargetReportableAutoRewardQuestAppliesXpAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1001, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1001, "REWARD", QuestVars: 0xF1, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 43,
			Name = "QuestFinishNpcTargetBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			TargetObjectId = QuestReportNpcObjectId,
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: QuestReportNpcObjectId,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1001,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => AssertQuestAction(packet, SmQuestAction.UpdateActionId, questId: 1001, statusValue: 5));
		Assert.Equal(300, player.Exp);
		var completedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, completedQuest);
		Assert.Equal("COMPLETE", completedQuest.Status);
		Assert.Equal(1, completedQuest.CompleteCount);
		Assert.Equal(0, completedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_NpcTargetUnhandledQuestDialogSendsDialogWindowFallback()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		var player = new Player
		{
			ObjectId = 44,
			Name = "QuestDialogFallbackBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			TargetObjectId = QuestReportNpcObjectId,
			Quests = [new PlayerQuestState(1001, "START", QuestVars: 0, Flags: 0, CompleteCount: 0)],
		};
		var packet = CreateDialogSelect(
			targetObjectId: QuestReportNpcObjectId,
			dialogActionId: CmDialogSelect.Select1_1,
			questId: 1001,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDialogWindow(
				packet,
				QuestReportNpcObjectId,
				CmDialogSelect.Select1_1,
				questId: 1001));
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.Equal("START", unchangedQuest.Status);
		Assert.Equal(0, player.Exp);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_NpcTargetQuestAcceptSimpleStartsQuestAndClosesDialog()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.Contains(1001, fixture.StaticData.QuestNpcStarts.GetQuestNpc(QuestReportNpcTemplateId).OnQuestStart);
		var player = new Player
		{
			ObjectId = 45,
			Name = "QuestAcceptSimpleBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			TargetObjectId = QuestReportNpcObjectId,
			Quests = [],
		};
		var packet = CreateDialogSelect(
			targetObjectId: QuestReportNpcObjectId,
			dialogActionId: CmDialogSelect.QuestAcceptSimple,
			questId: 1001,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => AssertQuestAction(packet, SmQuestAction.AddActionId, questId: 1001, statusValue: 3),
			packet => AssertDialogWindow(packet, QuestReportNpcObjectId, expectedDialogPageId: 0, questId: 0));
		var startedQuest = Assert.Single(player.Quests);
		Assert.Equal(1001, startedQuest.QuestId);
		Assert.Equal("START", startedQuest.Status);
		Assert.Equal(0, startedQuest.QuestVars);
		Assert.Equal(0, startedQuest.CompleteCount);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_NpcTargetQuestAccept1StartsQuestAndSendsStartPage()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.Contains(1001, fixture.StaticData.QuestNpcStarts.GetQuestNpc(QuestReportNpcTemplateId).OnQuestStart);
		var player = new Player
		{
			ObjectId = 46,
			Name = "QuestAccept1Boundary",
			PlayerClass = "RANGER",
			Level = 1,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			TargetObjectId = QuestReportNpcObjectId,
			Quests = [],
		};
		var packet = CreateDialogSelect(
			targetObjectId: QuestReportNpcObjectId,
			dialogActionId: CmDialogSelect.QuestAccept1,
			questId: 1001,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => AssertQuestAction(packet, SmQuestAction.AddActionId, questId: 1001, statusValue: 3),
			packet => AssertDialogWindow(packet, QuestReportNpcObjectId, expectedDialogPageId: 1003, questId: 1001));
		var startedQuest = Assert.Single(player.Quests);
		Assert.Equal(1001, startedQuest.QuestId);
		Assert.Equal("START", startedQuest.Status);
		Assert.Equal(0, startedQuest.QuestVars);
		Assert.Equal(0, startedQuest.CompleteCount);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesKinahAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1002, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1002, "REWARD", QuestVars: 0x34, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1002,
			Name = "QuestFinishKinahBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem { ObjectId = 5001, ItemId = KinahItemId, Count = 25, OwnerId = 1002, Location = 0 },
			],
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1002,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var inventoryUpdate = Assert.IsType<SmInventoryUpdateItem>(packet);
				Assert.Equal(SmInventoryUpdateItem.IncreaseKinahQuest, inventoryUpdate.UpdateType);
			},
			packet => Assert.IsType<SmQuestAction>(packet));
		var kinahItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, kinahItem.ObjectId);
		Assert.Equal(KinahItemId, kinahItem.ItemId);
		Assert.Equal(125, kinahItem.Count);
		Assert.Equal(0, player.Exp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAddsFixedItemAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1003, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1003, "REWARD", QuestVars: 0x56, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1003,
			Name = "QuestFinishItemBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1003,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(RewardItemId, rewardItem.ItemId);
		Assert.Equal(2, rewardItem.Count);
		Assert.Equal(player.ObjectId, rewardItem.OwnerId);
		Assert.Equal(0, rewardItem.Location);
		Assert.Equal(0, player.Exp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAddsSelectedItemAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1011, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1011, "REWARD", QuestVars: 0x57, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1012,
			Name = "QuestFinishSelectableBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward1,
			questId: 1011,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(SelectableRewardItemId, rewardItem.ItemId);
		Assert.Equal(3, rewardItem.Count);
		Assert.Equal(player.ObjectId, rewardItem.OwnerId);
		Assert.Equal(0, rewardItem.Location);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAddsClassSelectedItemOnLastRepeatAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1012, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1012, "REWARD", QuestVars: 0x58, Flags: 0, CompleteCount: 4);
		var player = new Player
		{
			ObjectId = 1013,
			Name = "QuestFinishClassSelectableBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward1,
			questId: 1012,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(ClassSelectableRewardItemId, rewardItem.ItemId);
		Assert.Equal(4, rewardItem.Count);
		Assert.Equal(player.ObjectId, rewardItem.OwnerId);
		Assert.Equal(0, rewardItem.Location);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(5, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAddsExtendedSelectedItemOnLastRepeatAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1014, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1014, "REWARD", QuestVars: 0x59, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1015,
			Name = "QuestFinishExtendedSelectableBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1014,
			extendedRewardIndex: 8);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(ExtendedSelectableRewardItemId, rewardItem.ItemId);
		Assert.Equal(6, rewardItem.Count);
		Assert.Equal(player.ObjectId, rewardItem.OwnerId);
		Assert.Equal(0, rewardItem.Location);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestRemovesQuestWorkItemsBeforeCompletion()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1004, out var lookupEntry));
		Assert.NotNull(lookupEntry);
		Assert.True(lookupEntry.Template.HasQuestWorkItems);

		var rewardQuestState = new PlayerQuestState(1004, "REWARD", QuestVars: 0x78, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1004,
			Name = "QuestFinishWorkItemBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem { ObjectId = 6001, ItemId = WorkItemId, Count = 4, OwnerId = 1004, Location = 0 },
				new InventoryItem { ObjectId = 6002, ItemId = WorkItemId, Count = 1, OwnerId = 1004, Location = 0 },
				new InventoryItem { ObjectId = 6003, ItemId = RewardItemId, Count = 1, OwnerId = 1004, Location = 0 },
			],
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1004,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => AssertQuestWorkItemDelete(packet, 6001),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => AssertQuestWorkItemDelete(packet, 6002),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		var remainingItem = Assert.Single(player.InventoryItems);
		Assert.Equal(6003, remainingItem.ObjectId);
		Assert.Equal([6001, 6002], player.DeletedInventoryItems.Select(item => item.ObjectId).Order());
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAddsTitleAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1005, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1005, "REWARD", QuestVars: 0x9A, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1005,
			Name = "QuestFinishTitleBoundary",
			PlayerClass = "RANGER",
			Race = "ELYOS",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1005,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300035, message.MessageId);
			},
			packet => AssertTitleListPacket(packet, RewardTitleId),
			packet => Assert.IsType<SmQuestAction>(packet));
		var title = Assert.Single(player.Titles);
		Assert.Equal(RewardTitleId, title.Id);
		Assert.Equal(0, title.ExpireTimeSeconds);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestWithKnownTitleSendsDuplicateMessageAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1005, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1005, "REWARD", QuestVars: 0xBC, Flags: 0, CompleteCount: 0);
		var knownTitle = new PlayerTitle(RewardTitleId, ExpireTimeSeconds: 0);
		var player = new Player
		{
			ObjectId = 1006,
			Name = "QuestFinishKnownTitleBoundary",
			PlayerClass = "RANGER",
			Race = "ELYOS",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Titles = [knownTitle],
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1005,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(901714, message.MessageId);
			},
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Same(knownTitle, Assert.Single(player.Titles));
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesAbyssPointsAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1006, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1006, "REWARD", QuestVars: 0xDE, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1007,
			Name = "QuestFinishApBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			AbyssRank = PlayerAbyssRank.Default(),
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1006,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1320000, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(200, player.AbyssRank.Ap);
		Assert.Equal(200, player.AbyssRank.DailyAp);
		Assert.Equal(200, player.AbyssRank.WeeklyAp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesDivinePointsAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1007, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1007, "REWARD", QuestVars: 0xEF, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1008,
			Name = "QuestFinishDpBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Dp = 100,
			IsOnline = true,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1007,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.RegistryPackets,
			packet => Assert.IsType<SmDpInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmStatUpdateDp>(packet));
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(700, player.Dp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesGloryPointsAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1008, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1008, "REWARD", QuestVars: 0xF1, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1009,
			Name = "QuestFinishGpBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			AbyssRank = PlayerAbyssRank.Default() with { Gp = 100, DailyGp = 5, WeeklyGp = 10 },
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1008,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1402081, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(150, player.AbyssRank.Gp);
		Assert.Equal(55, player.AbyssRank.DailyGp);
		Assert.Equal(60, player.AbyssRank.WeeklyGp);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesCubeExpansionAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1009, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1009, "REWARD", QuestVars: 0xF2, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1010,
			Name = "QuestFinishCubeBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			NpcExpands = 2,
			QuestExpands = 1,
			ItemExpands = 3,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1009,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300431, message.MessageId);
			},
			packet => AssertCubeSize(packet, expectedNpcExpands: 2, expectedQuestExpands: 2, expectedItemExpands: 3),
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(2, player.QuestExpands);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestAppliesWarehouseExpansionAndCompletesQuest()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1010, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1010, "REWARD", QuestVars: 0xF3, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1011,
			Name = "QuestFinishWarehouseBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			WarehouseNpcExpands = 2,
			WarehouseBonusExpands = 1,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1010,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300433, message.MessageId);
			},
			packet => AssertWarehouseInfo(packet, expectedWarehouseType: RegularWarehouse, expectedFirstPacket: false, expectedExpandLevel: 4),
			packet => AssertWarehouseInfo(packet, expectedWarehouseType: AccountWarehouse, expectedFirstPacket: false, expectedExpandLevel: 0),
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.Equal(2, player.WarehouseBonusExpands);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestCompletesNpcFactionAfterQuestUpdate()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1013, out var lookupEntry));
		Assert.NotNull(lookupEntry);

		var rewardQuestState = new PlayerQuestState(1013, "REWARD", QuestVars: 0xF4, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1014,
			Name = "QuestFinishNpcFactionBoundary",
			PlayerClass = "RANGER",
			Level = 1,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			NpcFactions = new PlayerNpcFactionsSnapshot(
			[
				new PlayerNpcFactionState(
					FactionId: 2,
					IsActive: true,
					IsMentor: false,
					TimeEpochSeconds: 0,
					State: PlayerNpcFactionQuestState.Start,
					QuestId: 1013),
			]),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1013,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => Assert.IsType<SmQuestAction>(packet));
		Assert.True(player.NpcFactions.TryGetFaction(2, out var faction));
		Assert.NotNull(faction);
		Assert.Equal(PlayerNpcFactionQuestState.Complete, faction.State);
		Assert.True(faction.TimeEpochSeconds > 0);
		var unchangedQuest = Assert.Single(player.Quests);
		Assert.NotSame(rewardQuestState, unchangedQuest);
		Assert.Equal("COMPLETE", unchangedQuest.Status);
		Assert.Equal(1, unchangedQuest.CompleteCount);
		Assert.Equal(0, unchangedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestStartsDefaultCompletionFollowUpAfterQuestUpdate()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1015, out var lookupEntry));
		Assert.NotNull(lookupEntry);
		Assert.Contains(
			fixture.StaticData.QuestCompletionFollowUps.Registrations,
			registration => registration.QuestId == 1016 && registration.PreQuestIds.SequenceEqual([1015]));

		var rewardQuestState = new PlayerQuestState(1015, "REWARD", QuestVars: 0xF5, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1016,
			Name = "QuestFinishCallbackBoundary",
			PlayerClass = "RANGER",
			Level = 10,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1015,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => AssertQuestAction(packet, SmQuestAction.UpdateActionId, questId: 1015, statusValue: 5),
			packet => AssertQuestAction(packet, SmQuestAction.AddActionId, questId: 1016, statusValue: 3));
		Assert.Equal(2, player.Quests.Count);
		var completedQuest = Assert.Single(player.Quests, quest => quest.QuestId == 1015);
		Assert.NotSame(rewardQuestState, completedQuest);
		Assert.Equal("COMPLETE", completedQuest.Status);
		var followUpQuest = Assert.Single(player.Quests, quest => quest.QuestId == 1016);
		Assert.Equal("START", followUpQuest.Status);
		Assert.Equal(0, followUpQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestLocksDefaultCompletionFollowUpMissionForPartialChain()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1017, out var lookupEntry));
		Assert.NotNull(lookupEntry);
		Assert.Contains(
			fixture.StaticData.QuestCompletionFollowUps.Registrations,
			registration => registration.QuestId == 1018 && registration.PreQuestIds.SequenceEqual([1016, 1017]));

		var rewardQuestState = new PlayerQuestState(1017, "REWARD", QuestVars: 0xF6, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1017,
			Name = "QuestFinishCallbackLockBoundary",
			PlayerClass = "RANGER",
			Level = 10,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1017,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => AssertQuestAction(packet, SmQuestAction.UpdateActionId, questId: 1017, statusValue: 5),
			packet => AssertQuestAction(packet, SmQuestAction.AddActionId, questId: 1018, statusValue: 6));
		Assert.Equal(2, player.Quests.Count);
		var completedQuest = Assert.Single(player.Quests, quest => quest.QuestId == 1017);
		Assert.Equal("COMPLETE", completedQuest.Status);
		var lockedQuest = Assert.Single(player.Quests, quest => quest.QuestId == 1018);
		Assert.Equal("LOCKED", lockedQuest.Status);
		Assert.Equal(0, lockedQuest.QuestVars);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_ReportableAutoRewardQuestLocksDefaultCompletionFollowUpMissionForRecursiveXmlChain()
	{
		await using var fixture = await QuestFinishDialogFixture.CreateAsync();
		Assert.True(fixture.StaticData.QuestFinishRewardProjections.TryGetQuest(1019, out var lookupEntry));
		Assert.NotNull(lookupEntry);
		Assert.Contains(
			fixture.StaticData.QuestCompletionFollowUps.Registrations,
			registration => registration.QuestId == 1021 && registration.PreQuestIds.Count == 0);

		var rewardQuestState = new PlayerQuestState(1019, "REWARD", QuestVars: 0xF7, Flags: 0, CompleteCount: 0);
		var player = new Player
		{
			ObjectId = 1019,
			Name = "QuestFinishCallbackXmlLockBoundary",
			PlayerClass = "RANGER",
			Level = 10,
			Exp = 0,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Quests = [rewardQuestState],
		};
		var packet = CreateDialogSelect(
			targetObjectId: 0,
			dialogActionId: SelectedQuestAutoReward,
			questId: 1019,
			extendedRewardIndex: 0);

		await fixture.Connection.HandleDialogSelectAsync(player, packet);

		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1370002, message.MessageId);
			},
			packet => AssertQuestAction(packet, SmQuestAction.UpdateActionId, questId: 1019, statusValue: 5),
			packet => AssertQuestAction(packet, SmQuestAction.AddActionId, questId: 1021, statusValue: 6));
		Assert.Equal(2, player.Quests.Count);
		var completedQuest = Assert.Single(player.Quests, quest => quest.QuestId == 1019);
		Assert.Equal("COMPLETE", completedQuest.Status);
		var lockedQuest = Assert.Single(player.Quests, quest => quest.QuestId == 1021);
		Assert.Equal("LOCKED", lockedQuest.Status);
		Assert.Equal(0, lockedQuest.QuestVars);
	}

	private static CmDialogSelect CreateDialogSelect(
		int targetObjectId,
		int dialogActionId,
		int questId,
		int extendedRewardIndex)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(targetObjectId);
		writer.WriteH(dialogActionId);
		writer.WriteH(extendedRewardIndex);
		writer.WriteH(0);
		writer.WriteD(questId);
		writer.WriteH(0);
		var packet = new CmDialogSelect(56, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static void AssertQuestWorkItemDelete(GameServerPacket packet, int expectedObjectId)
	{
		var deletePacket = Assert.IsType<SmDeleteItem>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(deletePacket));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertTitleListPacket(GameServerPacket packet, int expectedTitleId)
	{
		var titleInfo = Assert.IsType<SmTitleInfo>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(titleInfo));
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedTitleId, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertCubeSize(
		GameServerPacket packet,
		int expectedNpcExpands,
		int expectedQuestExpands,
		int expectedItemExpands)
	{
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(cubeUpdate));
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedNpcExpands, (int)reader.ReadC());
		Assert.Equal(expectedQuestExpands, (int)reader.ReadC());
		Assert.Equal(expectedItemExpands, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertWarehouseInfo(
		GameServerPacket packet,
		int expectedWarehouseType,
		bool expectedFirstPacket,
		int expectedExpandLevel)
	{
		var warehouseInfo = Assert.IsType<SmWarehouseInfo>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(warehouseInfo));
		Assert.Equal(expectedWarehouseType, (int)reader.ReadC());
		Assert.Equal(expectedFirstPacket ? 1 : 0, (int)reader.ReadC());
		Assert.Equal(expectedExpandLevel, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertQuestAction(
		GameServerPacket packet,
		int expectedActionId,
		int questId,
		int statusValue)
	{
		var questAction = Assert.IsType<SmQuestAction>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(questAction));
		Assert.Equal(expectedActionId, (int)reader.ReadC());
		Assert.Equal(questId, reader.ReadD());
		Assert.Equal(statusValue, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertDialogWindow(
		GameServerPacket packet,
		int expectedTargetObjectId,
		int expectedDialogPageId,
		int questId)
	{
		var dialogWindow = Assert.IsType<SmDialogWindow>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(dialogWindow));
		Assert.Equal(expectedTargetObjectId, reader.ReadD());
		Assert.Equal(expectedDialogPageId, reader.ReadH());
		Assert.Equal(questId, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class QuestFinishDialogFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly string _tempRoot;

		private QuestFinishDialogFixture(
			TcpClient client,
			GameServerConnection connection,
			StaticData staticData,
			List<GameServerPacket> sentPackets,
			CapturingConnectionRegistry connectionRegistry,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			StaticData = staticData;
			SentPackets = sentPackets;
			ConnectionRegistry = connectionRegistry;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public StaticData StaticData { get; }

		public List<GameServerPacket> SentPackets { get; }

		public CapturingConnectionRegistry ConnectionRegistry { get; }

		public List<GameServerPacket> RegistryPackets => ConnectionRegistry.PacketOrder;

		public static async Task<QuestFinishDialogFixture> CreateAsync()
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-quest-finish-dialog-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			var questHandlerDirectory = Path.Combine(tempRoot, "game-server", "data", "handlers", "quest", "test");
			Directory.CreateDirectory(questHandlerDirectory);
			await File.WriteAllTextAsync(
				Path.Combine(questHandlerDirectory, "_1016FollowUp.java"),
				"""
				package quest.test;

				import com.aionemu.gameserver.questEngine.handlers.AbstractQuestHandler;
				import com.aionemu.gameserver.questEngine.model.QuestEnv;

				public class _1016FollowUp extends AbstractQuestHandler {
					public _1016FollowUp() {
						super(1016);
					}

					@Override
					public void register() {
						qe.registerOnQuestCompleted(questId);
					}

					@Override
					public void onQuestCompletedEvent(QuestEnv env) {
						defaultOnQuestCompletedEvent(env, 1015);
					}
				}
				""");
			await File.WriteAllTextAsync(
				Path.Combine(questHandlerDirectory, "_1018LockedFollowUp.java"),
				"""
				package quest.test;

				import com.aionemu.gameserver.questEngine.handlers.AbstractQuestHandler;
				import com.aionemu.gameserver.questEngine.model.QuestEnv;

				public class _1018LockedFollowUp extends AbstractQuestHandler {
					public _1018LockedFollowUp() {
						super(1018);
					}

					@Override
					public void register() {
						qe.registerOnQuestCompleted(questId);
					}

					@Override
					public void onQuestCompletedEvent(QuestEnv env) {
						defaultOnQuestCompletedEvent(env, 1016, 1017);
					}
				}
				""");
			await File.WriteAllTextAsync(
				Path.Combine(questHandlerDirectory, "_1021XmlLockedFollowUp.java"),
				"""
				package quest.test;

				import com.aionemu.gameserver.questEngine.handlers.AbstractQuestHandler;
				import com.aionemu.gameserver.questEngine.model.QuestEnv;

				public class _1021XmlLockedFollowUp extends AbstractQuestHandler {
					public _1021XmlLockedFollowUp() {
						super(1021);
					}

					@Override
					public void register() {
						qe.registerOnQuestCompleted(questId);
					}

					@Override
					public void onQuestCompletedEvent(QuestEnv env) {
						defaultOnQuestCompletedEvent(env);
					}
				}
				""");
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<player_experience_table>
						<exp>0</exp>
						<exp>1000</exp>
						<exp>3000</exp>
					</player_experience_table>
					<item_templates>
						<item_template id="182400001" name="Kinah" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="2147483647" price="1" />
						<item_template id="182200003" name="Quest Work Item" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100" price="1" />
						<item_template id="186000001" name="Quest Reward Item" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100" price="1" />
						<item_template id="186000002" name="Selectable Quest Reward Item" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100" price="1" />
						<item_template id="186000003" name="Class Selectable Quest Reward Item" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100" price="1" />
						<item_template id="186000004" name="Extended Selectable Quest Reward Item" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100" price="1" />
					</item_templates>
					<player_titles>
						<title id="5" nameId="412994" desc="1" race="ELYOS" />
					</player_titles>
					<quests>
						<quest id="1001" can_report="true" reward_repeat_count="1" start_npc_ids="203001">
							<rewards exp="300" />
						</quest>
						<quest id="1002" can_report="true" reward_repeat_count="1">
							<rewards gold="100" />
						</quest>
						<quest id="1003" can_report="true" reward_repeat_count="1">
							<rewards>
								<reward_item item_id="186000001" count="2" />
							</rewards>
						</quest>
						<quest id="1004" can_report="true" reward_repeat_count="1">
							<quest_work_items>
								<quest_work_item item_id="182200003" count="1" />
							</quest_work_items>
							<rewards exp="50" />
						</quest>
						<quest id="1005" can_report="true" reward_repeat_count="1">
							<rewards title="5" />
						</quest>
						<quest id="1006" can_report="true" reward_repeat_count="1">
							<rewards ap="200" />
						</quest>
						<quest id="1007" can_report="true" reward_repeat_count="1">
							<rewards dp="600" />
						</quest>
						<quest id="1008" can_report="true" reward_repeat_count="1">
							<rewards gp="50" />
						</quest>
						<quest id="1009" can_report="true" reward_repeat_count="1">
							<rewards extend_inventory="1" />
						</quest>
						<quest id="1010" can_report="true" reward_repeat_count="1">
							<rewards extend_inventory="2" />
						</quest>
						<quest id="1011" can_report="true" reward_repeat_count="1">
							<rewards>
								<selectable_reward_item item_id="186000002" count="3" />
							</rewards>
						</quest>
						<quest id="1012" can_report="true" reward_repeat_count="5" use_class_reward="2">
							<rewards />
							<ranger_selectable_reward item_id="186000003" count="4" />
						</quest>
						<quest id="1013" can_report="true" reward_repeat_count="1" npcfaction_id="2">
							<rewards exp="1" />
						</quest>
						<quest id="1014" can_report="true" reward_repeat_count="1">
							<extended_rewards>
								<selectable_reward_item item_id="186000004" count="6" />
							</extended_rewards>
						</quest>
						<quest id="1015" can_report="true" reward_repeat_count="1">
							<rewards exp="1" />
						</quest>
						<quest id="1016" minlevel_permitted="1">
							<rewards />
						</quest>
						<quest id="1017" can_report="true" reward_repeat_count="1">
							<rewards exp="1" />
						</quest>
						<quest id="1018" category="MISSION" minlevel_permitted="1">
							<rewards />
						</quest>
						<quest id="1019" can_report="true" reward_repeat_count="1">
							<rewards exp="1" />
						</quest>
						<quest id="1020">
							<start_conditions>
								<finished quest_id="1019" />
							</start_conditions>
							<rewards />
						</quest>
						<quest id="1021" category="MISSION" minlevel_permitted="1">
							<start_conditions>
								<finished quest_id="1020" />
							</start_conditions>
							<rewards />
						</quest>
					</quests>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager(dataManager);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			world.Initialize();
			world.TryAddObject(
				QuestReportNpcObjectId,
				new WorldNpc(
					QuestReportNpcObjectId,
					QuestReportNpcTemplateId,
					new NpcTemplateSummary(
						QuestReportNpcTemplateId,
						"quest_report_npc",
						NameId: 0,
						Level: 1,
						Rank: "NORMAL",
						Rating: "NORMAL",
						Race: "NONE",
						Tribe: "NONE",
						Type: "NPC"),
					new WorldPosition(210010000, 1, 2, 3, 0)));
			var sentPackets = new List<GameServerPacket>();
			var connectionRegistry = new CapturingConnectionRegistry();

			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"quest-finish-dialog-boundary-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					world: world,
					connectionRegistry: connectionRegistry,
					idFactory: new IDFactory(),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new QuestFinishDialogFixture(client, connection, dataManager.StaticData, sentPackets, connectionRegistry, tempRoot);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<GameServerPacket> PacketOrder { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			PacketOrder.Add(packet);
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			PacketOrder.Add(packet);
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}
