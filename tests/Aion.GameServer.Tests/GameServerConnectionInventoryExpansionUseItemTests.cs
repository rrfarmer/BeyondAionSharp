using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionInventoryExpansionUseItemTests
{
	private readonly ITestOutputHelper _output;

	public GameServerConnectionInventoryExpansionUseItemTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public async Task HandleUseItemAsync_CubeExpansionTicketConsumesItemAndRefreshesCubeSize()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169630000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, player.ItemExpands);
		Assert.Equal(36, InventoryCapacity.GetCubeLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_WarehouseExpansionTicketConsumesItemAndRefreshesWarehouseInfo()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169640000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, player.WarehouseBonusExpands);
		Assert.Equal(32, InventoryCapacity.GetWarehouseLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_WarehouseExpansionTicketAllowsQuestOffsetLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169640000);
		player.WarehouseBonusExpands = 1;
		player.Quests = [new PlayerQuestState(1987, "COMPLETE", 0, 0, 0)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(2, player.WarehouseBonusExpands);
		Assert.Equal(40, InventoryCapacity.GetWarehouseLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_CraftLearnTicketWritesCleanupSealFlagForRemainingSource()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 152200001);
		player.Skills = [new PlayerSkill { SkillId = 40009, SkillLevel = 1 }];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Contains(155000001, player.Recipes);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmLearnRecipe>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmItemUsageAnimation>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_SkillBookDirectDeletesSourceLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169500001);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.InventoryItems);
		Assert.Contains(player.Skills, skill => skill.SkillId == 1 && skill.SkillLevel == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169500001, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSkillList>(packet),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0));
	}

	[Fact]
	public async Task HandleUseItemAsync_TitleCardDirectDeletesSourceLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169945000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.InventoryItems);
		Assert.Contains(player.Titles, title => title.Id == 269);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169945000, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmTitleInfo>(packet),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0));
	}

	[Fact]
	public async Task HandleUseItemAsync_EmotionCardDirectDeletesSourceLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169600001);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.InventoryItems);
		Assert.Contains(player.Emotions, emotion => emotion.Id == 64);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169600001, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmEmotionList>(packet),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemPersistsQuestAndSendsQuestAdd()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700001);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		var questState = Assert.Single(player.Quests);
		Assert.Equal(new PlayerQuestState(1114, "START", 0, 0, 0), questState);
		Assert.Equal(1, repository.InsertPlayerQuestCalls);
		Assert.Equal(questState, repository.InsertedPlayerQuestState);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169700001, expectedEnd: 1, expectedUnknown3: 1),
			packet => AssertQuestActionPacket(Assert.IsType<SmQuestAction>(packet), SmQuestAction.AddActionId, 1114));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemRestartsCompletedRepeatableQuest()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700002);
		player.Quests =
		[
			new PlayerQuestState(1115, "COMPLETE", 23, 1, 1, RewardGroup: 2, CompleteTime: DateTimeOffset.UnixEpoch),
		];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		var questState = Assert.Single(player.Quests);
		Assert.Equal("START", questState.Status);
		Assert.Equal(23, questState.QuestVars);
		Assert.Equal(1, questState.Flags);
		Assert.Equal(1, questState.CompleteCount);
		Assert.Equal(2, questState.RewardGroup);
		Assert.Equal(1, repository.UpdatePlayerQuestCalls);
		Assert.Equal(questState, repository.UpdatedPlayerQuestState);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169700002, expectedEnd: 1, expectedUnknown3: 1),
			packet => AssertQuestActionPacket(Assert.IsType<SmQuestAction>(packet), SmQuestAction.AddActionId, 1115, expectedClientQuestVars: 23 | (1 << 24)));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemSendsWorkingQuestMessageForActiveState()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700001);
		player.Quests = [new PlayerQuestState(1114, "START", 7, 0, 0)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		var questState = Assert.Single(player.Quests);
		Assert.Equal("START", questState.Status);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300597));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemSendsNoneRepeatableMessageForCompletedNonRepeatableState()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700001);
		player.Quests = [new PlayerQuestState(1114, "COMPLETE", 0, 0, 1)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		var questState = Assert.Single(player.Quests);
		Assert.Equal("COMPLETE", questState.Status);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300599, "Test Quest Starter Quest"));
	}

	[Theory]
	[InlineData(169700003, 1300575)]
	[InlineData(169700006, 1300580)]
	[InlineData(169700007, 1300579)]
	public async Task HandleUseItemAsync_QuestStartItemSendsFixedStartConditionFailureMessages(int itemId, int expectedMessageId)
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: itemId);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.Quests);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: expectedMessageId));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemSendsRankStartConditionFailureMessage()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700011);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.Quests);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(
				Assert.IsType<SmSystemMessage>(packet),
				expectedMessageId: 1300573,
				PlayerAbyssRank.GetRankL10n("ELYOS", 4)));
	}

	[Theory]
	[InlineData(169700004, 1300571, "10", 1)]
	[InlineData(169700005, 1300572, "2", 5)]
	public async Task HandleUseItemAsync_QuestStartItemSendsLevelStartConditionFailureMessages(
		int itemId,
		int expectedMessageId,
		string expectedLevel,
		int playerLevel)
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: itemId, level: playerLevel);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.Quests);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId, expectedLevel));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemSendsMaxNormalMessageWhenQuestListIsFull()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			options: CreateQuestLimitOptions(limit: 1));
		var player = CreatePlayer(itemId: 169700001);
		player.Quests = [new PlayerQuestState(1121, "START", 0, 0, 0)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Single(player.Quests);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300622));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemAllowsMembershipQuestLimitBypass()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			options: CreateQuestLimitOptions(limit: 1, disabledMembership: 5));
		var player = CreatePlayer(itemId: 169700001, accountMembership: 5);
		player.Quests = [new PlayerQuestState(1121, "START", 0, 0, 0)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(2, player.Quests.Count);
		Assert.Equal(1, repository.InsertPlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169700001, expectedEnd: 1, expectedUnknown3: 1),
			packet => AssertQuestActionPacket(Assert.IsType<SmQuestAction>(packet), SmQuestAction.AddActionId, 1114, expectedClientQuestVars: 0));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemAllowsNoCountQuestWhenQuestListIsFull()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			options: CreateQuestLimitOptions(limit: 1));
		var player = CreatePlayer(itemId: 169700008);
		player.Quests = [new PlayerQuestState(1121, "START", 0, 0, 0)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(2, player.Quests.Count);
		Assert.Equal(1, repository.InsertPlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 169700008, expectedEnd: 1, expectedUnknown3: 1),
			packet => AssertQuestActionPacket(Assert.IsType<SmQuestAction>(packet), SmQuestAction.AddActionId, 1122, expectedClientQuestVars: 0));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemSendsInventoryItemConditionFailureMessage()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700009);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.Quests);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		var requiredItemName = fixture.StaticData.ItemTemplates.GetItemTemplate(182215001)!.GetClientName()!;
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300594, requiredItemName));
	}

	[Fact]
	public async Task HandleUseItemAsync_QuestStartItemSendsCombineSkillConditionFailureMessage()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 169700010);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Empty(player.Quests);
		Assert.Equal(0, repository.InsertPlayerQuestCalls);
		Assert.Equal(0, repository.UpdatePlayerQuestCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300574, "199"));
	}

	[Theory]
	[InlineData(169630000)]
	[InlineData(169640000)]
	public async Task HandleUseItemAsync_InventoryExpansionPersistenceFailureDoesNotMutateRuntimeState(int itemId)
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveInventoryExpansionMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, repository.SaveInventoryExpansionMutationCalls);
		Assert.Equal(0, player.ItemExpands);
		Assert.Equal(0, player.WarehouseBonusExpands);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleUseItemAsync_ApExtractSendsAbyssPointsPlannerPackets()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(new EmptyPlayerEnterWorldRepository());
		var player = CreateApExtractPlayer();

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6001));

		Assert.Equal(980, player.AbyssRank.Ap);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 6001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6001, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1320000, "980"),
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_ApExtractHonorsConfiguredAbyssPointCap()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			options: CreateApCapOptions());
		var player = CreateApExtractPlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 900 };

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6001));

		Assert.Equal(1_000, player.AbyssRank.Ap);
		Assert.Equal(1_000, repository.ApExtractAbyssRank?.Ap);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 6001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6001, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1320000, "100"),
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_ApExtractDeletesLastToolWithUseDeleteAndCubeUpdate()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(new EmptyPlayerEnterWorldRepository());
		var player = CreateApExtractPlayer(sourceCount: 1);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6001));

		Assert.Equal(980, player.AbyssRank.Ap);
		Assert.Empty(player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6001, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1320000, "980"),
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public async Task HandleChargeItemAsync_ApPaymentSendsAbyssPointsPlannerPackets()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(new EmptyPlayerEnterWorldRepository());
		var player = CreateChargePaymentPlayer();

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItem(itemObjectId: 7001, chargeLevel: 1));

		Assert.Equal(500, player.AbyssRank.Ap);
		var chargedItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 7001);
		Assert.Equal(ItemChargeService.Level1ChargePoints, chargedItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "500"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	[Fact]
	public async Task HandleChargeItemAsync_SelectedEquippedItemCanBeChargedLikeJavaInventoryLookup()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(new EmptyPlayerEnterWorldRepository());
		var player = CreateChargePaymentPlayer(isEquipped: true);

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItem(itemObjectId: 7001, chargeLevel: 1));

		Assert.Equal(500, player.AbyssRank.Ap);
		var chargedItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 7001);
		Assert.True(chargedItem.IsEquipped);
		Assert.Equal(ItemChargeService.Level1ChargePoints, chargedItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "500"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7001),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401340));
	}

	[Fact]
	public async Task HandleChargeItemAsync_ApPaymentHonorsConfiguredAbyssPointCapClamp()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			options: CreateApCapOptions());
		var player = CreateChargePaymentPlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 1_600, Rank = 2, MaxRank = 2 };

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItem(itemObjectId: 7001, chargeLevel: 1));

		Assert.Equal(1_000, player.AbyssRank.Ap);
		Assert.Equal(1_000, repository.ChargePaymentAbyssRank?.Ap);
		var chargedItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 7001);
		Assert.Equal(ItemChargeService.Level1ChargePoints, chargedItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "600"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	[Fact]
	public async Task HandleChargeItemAsync_ApPaymentRejectsInsufficientAbyssPointsWithoutSideEffects()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargePaymentPlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 499 };

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItem(itemObjectId: 7001, chargeLevel: 1));

		Assert.Equal(499, player.AbyssRank.Ap);
		Assert.Null(repository.ChargePaymentAbyssRank);
		Assert.Equal(0, repository.SaveItemChargeMutationCalls);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7001);
		Assert.Equal(0, item.Charge);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleChargeItemAsync_ProcessesSelectedItemsInPacketOrderLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayerWithTwoItems();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 500 };

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItems(chargeLevel: 1, 7002, 7001));

		Assert.Equal(0, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeMutationCalls);
		Assert.Equal(0, repository.ChargePaymentAbyssRank?.Ap);
		var firstInventoryItem = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7001);
		var packetFirstItem = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7002);
		Assert.Equal(0, firstInventoryItem.Charge);
		Assert.Equal(ItemChargeService.Level1ChargePoints, packetFirstItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "500"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7002),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401335, "Test Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401340));
	}

	[Fact]
	public async Task HandleMoveItemAsync_StackableAutoSlotMergesDestinationStackLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 3);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 97,
					Location = 1,
				},
			])
			.ToArray();

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 1, slot: -1));

		var targetStack = Assert.Single(player.InventoryItems);
		Assert.Equal(6001, targetStack.ObjectId);
		Assert.Equal(100, targetStack.Count);
		Assert.Equal(1, targetStack.Location);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(0, savedMerge.SourceItem.Count);
		Assert.Equal(100, savedMerge.TargetItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedWarehouseType: 1,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0));
	}

	[Fact]
	public async Task HandleMoveItemAsync_FullWarehouseSourceAutoMergeUsesJavaStorageSize()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 3, location: 1);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 97,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 1, destination: 0, slot: -1));

		var targetStack = Assert.Single(player.InventoryItems);
		Assert.Equal(6001, targetStack.ObjectId);
		Assert.Equal(100, targetStack.Count);
		Assert.Equal(0, targetStack.Location);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(0, savedMerge.SourceItem.Count);
		Assert.Equal(100, savedMerge.TargetItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect),
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 1,
				expectedObjectId: 5001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_AccountWarehouseAutoMergeFillsExistingStackBeforeFullCheckLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 3, accountId: 77);
		player.AccountWarehouseItems = new[]
			{
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 97,
					OwnerId = 77,
					Location = 2,
					Slot = 12,
				},
			}
			.Concat(CreateStorageFillerItems(
				location: 2,
				count: InventoryCapacity.GetAccountWarehouseLimit() - 1,
				ownerId: 77,
				startObjectId: 7000))
			.ToArray();

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 2, slot: -1));

		Assert.Empty(player.InventoryItems);
		Assert.Equal(InventoryCapacity.GetAccountWarehouseLimit(), player.AccountWarehouseItems.Count);
		var targetStack = Assert.Single(player.AccountWarehouseItems, item => item.ObjectId == 6001);
		Assert.Equal(100, targetStack.Count);
		Assert.Equal(77, targetStack.OwnerId);
		Assert.Equal(2, targetStack.Location);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(1001, savedMerge.SourceItem.OwnerId);
		Assert.Equal(0, savedMerge.SourceItem.Count);
		Assert.Equal(77, savedMerge.TargetItem.OwnerId);
		Assert.Equal(100, savedMerge.TargetItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedWarehouseType: 2,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect),
			packet => AssertDeleteItemPayload(
				Assert.IsType<SmDeleteItem>(packet),
				expectedObjectId: 5001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0));
	}

	[Fact]
	public async Task HandleMoveItemAsync_PartialAutoSlotMergeMovesRemainingStackLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 5);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 97,
					Location = 1,
				},
			])
			.ToArray();

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 1, slot: -1));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(2, item.Count);
				Assert.Equal(1, item.Location);
				Assert.Equal(-1, item.Slot);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(100, item.Count);
				Assert.Equal(1, item.Location);
			});
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		Assert.Equal(1, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMove = Assert.NotNull(repository.SavedItemCrossStorageMoveMutation);
		Assert.Equal(1001, savedMove.PlayerObjectId);
		Assert.Equal(5001, savedMove.ItemObjectId);
		Assert.Equal(1, savedMove.NewLocation);
		Assert.Equal(-1, savedMove.NewSlot);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedWarehouseType: 1,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect),
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemSplitMove),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.ItemCollect),
			packet => Assert.IsType<SmCubeUpdate>(packet));
	}

	[Fact]
	public async Task HandleMoveItemAsync_CubeSourceMovesItemToRestoredRegularWarehouseLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 2);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 4;

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 1, slot: 9));

		Assert.Empty(player.InventoryItems);
		var movedItem = Assert.Single(player.WarehouseItems);
		Assert.Equal(5001, movedItem.ObjectId);
		Assert.Equal(1001, movedItem.OwnerId);
		Assert.Equal(1, movedItem.Location);
		Assert.Equal(9, movedItem.Slot);
		Assert.Equal(1, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMove = Assert.NotNull(repository.SavedItemCrossStorageMoveMutation);
		Assert.Equal(1001, savedMove.PlayerObjectId);
		Assert.Equal(5001, savedMove.ItemObjectId);
		Assert.Equal(1, savedMove.NewLocation);
		Assert.Equal(9, savedMove.NewSlot);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 2,
				expectedSlot: 9),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_RegularWarehouseSourceMovesRestoredItemToCubeLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 0, accountId: 77);
		player.InventoryItems = [];
		player.WarehouseItems =
		[
			new InventoryItem
			{
				ObjectId = 5001,
				ItemId = 200,
				Count = 2,
				OwnerId = 1001,
				Location = 1,
				Slot = 27,
			},
		];

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 1, destination: 0, slot: 8));

		Assert.Empty(player.WarehouseItems);
		var movedItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, movedItem.ObjectId);
		Assert.Equal(1001, movedItem.OwnerId);
		Assert.Equal(0, movedItem.Location);
		Assert.Equal(8, movedItem.Slot);
		Assert.Equal(1, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMove = Assert.NotNull(repository.SavedItemCrossStorageMoveMutation);
		Assert.Equal(1001, savedMove.PlayerObjectId);
		Assert.Equal(77, savedMove.AccountId);
		Assert.Equal(5001, savedMove.ItemObjectId);
		Assert.Equal(1, savedMove.OldLocation);
		Assert.Equal(0, savedMove.NewLocation);
		Assert.Equal(8, savedMove.NewSlot);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 1,
				expectedObjectId: 5001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 1),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 200,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 8),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_AccountWarehouseSourceMovesRestoredItemToCubeLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 0, accountId: 77);
		player.InventoryItems = [];
		player.AccountWarehouseItems =
		[
			new InventoryItem
			{
				ObjectId = 5001,
				ItemId = 200,
				Count = 2,
				OwnerId = 77,
				Location = 2,
				Slot = 6,
			},
		];

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 2, destination: 0, slot: 9));

		Assert.Empty(player.AccountWarehouseItems);
		var movedItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, movedItem.ObjectId);
		Assert.Equal(1001, movedItem.OwnerId);
		Assert.Equal(0, movedItem.Location);
		Assert.Equal(9, movedItem.Slot);
		Assert.Equal(1, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMove = Assert.NotNull(repository.SavedItemCrossStorageMoveMutation);
		Assert.Equal(1001, savedMove.PlayerObjectId);
		Assert.Equal(77, savedMove.AccountId);
		Assert.Equal(5001, savedMove.ItemObjectId);
		Assert.Equal(2, savedMove.OldLocation);
		Assert.Equal(0, savedMove.NewLocation);
		Assert.Equal(9, savedMove.NewSlot);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 2,
				expectedObjectId: 5001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 200,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 9),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_CubeSourceMovesItemToAccountWarehouseOwnerLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 2, accountId: 77);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 4;

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 2, slot: 12));

		Assert.Empty(player.InventoryItems);
		var movedItem = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(5001, movedItem.ObjectId);
		Assert.Equal(77, movedItem.OwnerId);
		Assert.Equal(2, movedItem.Location);
		Assert.Equal(12, movedItem.Slot);
		Assert.Equal(1, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMove = Assert.NotNull(repository.SavedItemCrossStorageMoveMutation);
		Assert.Equal(1001, savedMove.PlayerObjectId);
		Assert.Equal(77, savedMove.AccountId);
		Assert.Equal(5001, savedMove.ItemObjectId);
		Assert.Equal(0, savedMove.OldLocation);
		Assert.Equal(2, savedMove.NewLocation);
		Assert.Equal(12, savedMove.NewSlot);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 2,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2));
	}

	[Fact]
	public async Task ProcessPacketAsync_CubeSourceMovesItemToLegionWarehouseOwnerLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 2, accountId: 77);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 4;
		player.LegionId = 88;
		player.LegionRank = "VOLUNTEER";
		player.LegionVolunteerPermission = 0x1000;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(156, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteC(0);
				buffer.WriteC(3);
				buffer.WriteH(12);
			}));

		var movedItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, movedItem.ObjectId);
		Assert.Equal(88, movedItem.OwnerId);
		Assert.Equal(3, movedItem.Location);
		Assert.Equal(12, movedItem.Slot);
		Assert.Equal(1, repository.SaveItemCrossStorageMoveMutationCalls);
		var savedMove = Assert.NotNull(repository.SavedItemCrossStorageMoveMutation);
		Assert.Equal(1001, savedMove.PlayerObjectId);
		Assert.Equal(77, savedMove.AccountId);
		Assert.Equal(88, savedMove.LegionId);
		Assert.Equal(5001, savedMove.ItemObjectId);
		Assert.Equal(0, savedMove.OldLocation);
		Assert.Equal(3, savedMove.NewLocation);
		Assert.Equal(12, savedMove.NewSlot);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 3,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 2,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 3));
	}

	[Fact]
	public async Task HandleMoveItemAsync_AccountWarehouseSameStorageSlotPersistsWithAccountOwnerLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 0, accountId: 77);
		player.InventoryItems = [];
		player.AccountWarehouseItems =
		[
			new InventoryItem
			{
				ObjectId = 5001,
				ItemId = 200,
				Count = 2,
				OwnerId = 77,
				Location = 2,
				Slot = 6,
			},
		];

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 2, destination: 2, slot: 9));

		var item = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(9, item.Slot);
		Assert.Equal(77, item.OwnerId);
		Assert.Equal(1, repository.SaveInventoryItemSlotCalls);
		Assert.Equal((77, 5001, 9L), Assert.Single(repository.SavedInventoryItemSlots));
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleMoveItemAsync_FullCubeDestinationSendsJavaStorageFullMessageAndUnlocksSource()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 5, location: 1);
		player.InventoryItems = player.InventoryItems
			.Concat(CreateStorageFillerItems(location: 0, count: InventoryCapacity.GetCubeLimit(player)))
			.ToArray();

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 1, destination: 0, slot: 8));

		Assert.Equal(28, player.InventoryItems.Count);
		var sourceItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 5001);
		Assert.Equal(5, sourceItem.Count);
		Assert.Equal(1, sourceItem.Location);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1390149),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedItemId: 200,
				expectedCount: 5,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_FullWarehouseDestinationSendsJavaStorageFullMessageAndUnlocksSource()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 5);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		player.InventoryItems = player.InventoryItems
			.Concat(CreateStorageFillerItems(location: 1, count: InventoryCapacity.GetWarehouseLimit(player)))
			.ToArray();

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 1, slot: 8));

		Assert.Equal(25, player.InventoryItems.Count);
		var unchangedItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 5001);
		Assert.Equal(5, unchangedItem.Count);
		Assert.Equal(0, unchangedItem.Location);
		Assert.Equal(12, unchangedItem.Slot);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300421),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 200,
				expectedCount: 5,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_FullAccountWarehouseDestinationSendsJavaStorageFullMessageAndUnlocksSource()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 5, accountId: 77);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		player.AccountWarehouseItems = CreateStorageFillerItems(
			location: 2,
			count: InventoryCapacity.GetAccountWarehouseLimit(),
			ownerId: 77);

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 2, slot: 8));

		var unchangedItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, unchangedItem.ObjectId);
		Assert.Equal(5, unchangedItem.Count);
		Assert.Equal(0, unchangedItem.Location);
		Assert.Equal(12, unchangedItem.Slot);
		Assert.Equal(InventoryCapacity.GetAccountWarehouseLimit(), player.AccountWarehouseItems.Count);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300421),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 200,
				expectedCount: 5,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleMoveItemAsync_RegularWarehouseRestrictionSendsDenialAndUnlocksSourceLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 999900202, count: 1);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;

		await InvokeHandleMoveItemAsync(
			fixture.Connection,
			player,
			CreateMoveItem(itemObjectId: 5001, source: 0, destination: 1, slot: 9));

		var unchangedItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, unchangedItem.ObjectId);
		Assert.Equal(0, unchangedItem.Location);
		Assert.Equal(12, unchangedItem.Slot);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Equal(0, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300418),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 999900202,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_FullSourceMergeDeletesSourceStackLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 3);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 97,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 3,
				sourceStorageType: 0,
				destinationItemObjectId: 6001,
				destinationStorageType: 0,
				slotNumber: 0));

		var targetStack = Assert.Single(player.InventoryItems);
		Assert.Equal(6001, targetStack.ObjectId);
		Assert.Equal(100, targetStack.Count);
		Assert.Equal(0, targetStack.Location);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(0, savedMerge.SourceItem.Count);
		Assert.Equal(100, savedMerge.TargetItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemMerge),
			packet => AssertDeleteItemPayload(
				Assert.IsType<SmDeleteItem>(packet),
				expectedObjectId: 5001,
				expectedDeleteType: SmDeleteItem.SplitDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_FullWarehouseSourceMergeUsesJavaStorageSize()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 3, location: 1);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 97,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 3,
				sourceStorageType: 1,
				destinationItemObjectId: 6001,
				destinationStorageType: 0,
				slotNumber: 0));

		var targetStack = Assert.Single(player.InventoryItems);
		Assert.Equal(6001, targetStack.ObjectId);
		Assert.Equal(100, targetStack.Count);
		Assert.Equal(0, targetStack.Location);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(0, savedMerge.SourceItem.Count);
		Assert.Equal(100, savedMerge.TargetItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect),
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 1,
				expectedObjectId: 5001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_CrossStorageRestrictionUnlocksSourceLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 166000030, count: 3);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 166000030,
					Count = 97,
					Location = 1,
				},
			])
			.ToArray();

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 3,
				sourceStorageType: 0,
				destinationItemObjectId: 6001,
				destinationStorageType: 1,
				slotNumber: 0));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(3, item.Count);
				Assert.Equal(0, item.Location);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(97, item.Count);
				Assert.Equal(1, item.Location);
			});
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300418),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 166000030,
				expectedCount: 3,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_WarehouseSourceRestrictionUnlockUsesJavaStorageSize()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 166000030, count: 3, location: 1);

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 1,
				sourceStorageType: 1,
				destinationItemObjectId: 0,
				destinationStorageType: 2,
				slotNumber: 0));

		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, sourceItem.ObjectId);
		Assert.Equal(3, sourceItem.Count);
		Assert.Equal(1, sourceItem.Location);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400356),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 166000030,
				expectedCount: 3,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_CrossStorageEmptySlotUsesJavaStorageSize()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 5, location: 1);

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 2,
				sourceStorageType: 1,
				destinationItemObjectId: 0,
				destinationStorageType: 0,
				slotNumber: 8));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(1, item.ObjectId);
				Assert.Equal(2, item.Count);
				Assert.Equal(0, item.Location);
				Assert.Equal(0, item.Slot);
			},
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(3, item.Count);
				Assert.Equal(1, item.Location);
			});
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 1,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemSplitMove),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 1,
				expectedItemId: 200,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_AccountWarehouseSourceSplitsRestoredItemToCubeLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 0, accountId: 77);
		player.InventoryItems = [];
		player.AccountWarehouseItems =
		[
			new InventoryItem
			{
				ObjectId = 5001,
				ItemId = 200,
				Count = 5,
				OwnerId = 77,
				Location = 2,
				Slot = 6,
			},
		];

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 2,
				sourceStorageType: 2,
				destinationItemObjectId: 0,
				destinationStorageType: 0,
				slotNumber: 8));

		var sourceItem = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(5001, sourceItem.ObjectId);
		Assert.Equal(77, sourceItem.OwnerId);
		Assert.Equal(3, sourceItem.Count);
		var newItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, newItem.ObjectId);
		Assert.Equal(1001, newItem.OwnerId);
		Assert.Equal(0, newItem.Location);
		Assert.Equal(0, newItem.Slot);
		Assert.Equal(2, newItem.Count);
		Assert.Equal(1, repository.SaveItemSplitMutationCalls);
		var savedSplit = Assert.NotNull(repository.SavedItemSplitMutation);
		Assert.Equal(1001, savedSplit.PlayerObjectId);
		Assert.Equal(77, savedSplit.SourceItem.OwnerId);
		Assert.Equal(3, savedSplit.SourceItem.Count);
		Assert.Equal(1001, savedSplit.NewItem.OwnerId);
		Assert.Equal(0, savedSplit.NewItem.Location);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 2,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemSplitMove),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 1,
				expectedItemId: 200,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task HandleSplitItemAsync_CubeSourceSplitsItemToAccountWarehouseOwnerLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 5, accountId: 77);

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 2,
				sourceStorageType: 0,
				destinationItemObjectId: 0,
				destinationStorageType: 2,
				slotNumber: 8));

		var sourceItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 5001);
		Assert.Equal(1001, sourceItem.OwnerId);
		Assert.Equal(3, sourceItem.Count);
		var newItem = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(1, newItem.ObjectId);
		Assert.Equal(77, newItem.OwnerId);
		Assert.Equal(2, newItem.Location);
		Assert.Equal(0, newItem.Slot);
		Assert.Equal(2, newItem.Count);
		Assert.Equal(1, repository.SaveItemSplitMutationCalls);
		var savedSplit = Assert.NotNull(repository.SavedItemSplitMutation);
		Assert.Equal(1001, savedSplit.PlayerObjectId);
		Assert.Equal(1001, savedSplit.SourceItem.OwnerId);
		Assert.Equal(3, savedSplit.SourceItem.Count);
		Assert.Equal(77, savedSplit.NewItem.OwnerId);
		Assert.Equal(2, savedSplit.NewItem.Location);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemSplitMove),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 1,
				expectedWarehouseType: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 2,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2));
	}

	[Fact]
	public async Task HandleSplitItemAsync_CubeKinahMoveCreatesMissingAccountWarehouseKinahLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: InventoryItemFactory.KinahItemId, count: 100, accountId: 77);

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 30,
				sourceStorageType: 0,
				destinationItemObjectId: 0,
				destinationStorageType: 2,
				slotNumber: 0));

		var sourceKinah = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, sourceKinah.ObjectId);
		Assert.Equal(70, sourceKinah.Count);
		Assert.Equal(1001, sourceKinah.OwnerId);
		Assert.Equal(0, sourceKinah.Location);
		var accountKinah = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(1, accountKinah.ObjectId);
		Assert.Equal(InventoryItemFactory.KinahItemId, accountKinah.ItemId);
		Assert.Equal(30, accountKinah.Count);
		Assert.Equal(77, accountKinah.OwnerId);
		Assert.Equal(2, accountKinah.Location);
		Assert.Equal(InventoryItemPersistentState.New, accountKinah.PersistentState);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(70, savedMerge.SourceItem.Count);
		Assert.Equal(30, savedMerge.TargetItem.Count);
		Assert.Equal(77, savedMerge.TargetItem.OwnerId);
		Assert.Equal(InventoryItemPersistentState.New, savedMerge.TargetItem.PersistentState);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemSplit),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 1,
				expectedWarehouseType: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: InventoryItemFactory.KinahItemId,
				expectedCount: 30,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2),
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 1,
				expectedWarehouseType: 2,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseKinahMerge));
	}

	[Fact]
	public async Task HandleSplitItemAsync_AccountWarehouseKinahMoveCreatesMissingCubeKinahLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: InventoryItemFactory.KinahItemId, count: 0, accountId: 77);
		player.InventoryItems = [];
		player.AccountWarehouseItems =
		[
			new InventoryItem
			{
				ObjectId = 5001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 100,
				OwnerId = 77,
				Location = 2,
			},
		];

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 40,
				sourceStorageType: 2,
				destinationItemObjectId: 0,
				destinationStorageType: 0,
				slotNumber: 0));

		var sourceKinah = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(5001, sourceKinah.ObjectId);
		Assert.Equal(60, sourceKinah.Count);
		Assert.Equal(77, sourceKinah.OwnerId);
		Assert.Equal(2, sourceKinah.Location);
		var cubeKinah = Assert.Single(player.InventoryItems);
		Assert.Equal(1, cubeKinah.ObjectId);
		Assert.Equal(InventoryItemFactory.KinahItemId, cubeKinah.ItemId);
		Assert.Equal(40, cubeKinah.Count);
		Assert.Equal(1001, cubeKinah.OwnerId);
		Assert.Equal(0, cubeKinah.Location);
		Assert.Equal(InventoryItemPersistentState.New, cubeKinah.PersistentState);
		Assert.Equal(1, repository.SaveItemMergeMutationCalls);
		var savedMerge = Assert.NotNull(repository.SavedItemMergeMutation);
		Assert.Equal(1001, savedMerge.PlayerObjectId);
		Assert.Equal(60, savedMerge.SourceItem.Count);
		Assert.Equal(40, savedMerge.TargetItem.Count);
		Assert.Equal(1001, savedMerge.TargetItem.OwnerId);
		Assert.Equal(InventoryItemPersistentState.New, savedMerge.TargetItem.PersistentState);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertWarehouseUpdatePayload(
				Assert.IsType<SmWarehouseUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 2,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemSplit),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 1,
				expectedItemId: InventoryItemFactory.KinahItemId,
				expectedCount: 40,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 1,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseKinahMerge));
	}

	[Fact]
	public async Task HandleSplitItemAsync_FullCubeDestinationSendsJavaStorageFullMessageWithoutMutation()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 5, location: 1);
		player.InventoryItems = player.InventoryItems
			.Concat(CreateStorageFillerItems(location: 0, count: InventoryCapacity.GetCubeLimit(player)))
			.ToArray();

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 2,
				sourceStorageType: 1,
				destinationItemObjectId: 0,
				destinationStorageType: 0,
				slotNumber: 8));

		Assert.Equal(28, player.InventoryItems.Count);
		var sourceItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 5001);
		Assert.Equal(5, sourceItem.Count);
		Assert.Equal(1, sourceItem.Location);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 1);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1390149));
	}

	[Fact]
	public async Task HandleSplitItemAsync_FullWarehouseDestinationSendsJavaStorageFullMessageWithoutMutation()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 5);
		player.InventoryItems = player.InventoryItems
			.Concat(CreateStorageFillerItems(location: 1, count: InventoryCapacity.GetWarehouseLimit(player)))
			.ToArray();

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 2,
				sourceStorageType: 0,
				destinationItemObjectId: 0,
				destinationStorageType: 1,
				slotNumber: 8));

		Assert.Equal(25, player.InventoryItems.Count);
		var sourceItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 5001);
		Assert.Equal(5, sourceItem.Count);
		Assert.Equal(0, sourceItem.Location);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 1);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300421));
	}

	[Fact]
	public async Task HandleSplitItemAsync_FullAccountWarehouseDestinationSendsJavaStorageFullMessageWithoutMutation()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 5, accountId: 77);
		player.AccountWarehouseItems = CreateStorageFillerItems(
			location: 2,
			count: InventoryCapacity.GetAccountWarehouseLimit(),
			ownerId: 77);

		await InvokeHandleSplitItemAsync(
			fixture.Connection,
			player,
			CreateSplitItem(
				sourceItemObjectId: 5001,
				itemAmount: 2,
				sourceStorageType: 0,
				destinationItemObjectId: 0,
				destinationStorageType: 2,
				slotNumber: 8));

		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, sourceItem.ObjectId);
		Assert.Equal(5, sourceItem.Count);
		Assert.Equal(0, sourceItem.Location);
		Assert.Equal(InventoryCapacity.GetAccountWarehouseLimit(), player.AccountWarehouseItems.Count);
		Assert.DoesNotContain(player.AccountWarehouseItems, item => item.ObjectId == 1);
		Assert.Equal(0, repository.SaveItemSplitMutationCalls);
		Assert.Equal(0, repository.SaveItemMergeMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300421));
	}

	[Fact]
	public async Task HandleReplaceItemAsync_CrossStorageSwitchDeletesThenAddsLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 1);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 201,
					Count = 2,
					Location = 1,
					Slot = 27,
				},
			])
			.ToArray();

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 0, sourceItemObjectId: 5001, replaceStorageType: 1, replaceItemObjectId: 6001));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(1, item.Location);
				Assert.Equal(27, item.Slot);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(0, item.Location);
				Assert.Equal(12, item.Slot);
			});
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Equal(1, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Equal((1001, 0, 0, 5001, 0, 1, 27L, 6001, 1, 0, 12L), repository.SavedItemStorageSwitchMutation);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 1,
				expectedObjectId: 6001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 6001,
				expectedItemId: 201,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 1,
				expectedSlot: 27),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleReplaceItemAsync_AccountWarehouseSwitchMovesRestoredRowsAndOwnersLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 1, accountId: 77);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		player.AccountWarehouseItems =
		[
			new InventoryItem
			{
				ObjectId = 6001,
				ItemId = 201,
				Count = 2,
				OwnerId = 77,
				Location = 2,
				Slot = 27,
			},
		];

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 0, sourceItemObjectId: 5001, replaceStorageType: 2, replaceItemObjectId: 6001));

		var accountItem = Assert.Single(player.AccountWarehouseItems);
		Assert.Equal(5001, accountItem.ObjectId);
		Assert.Equal(77, accountItem.OwnerId);
		Assert.Equal(2, accountItem.Location);
		Assert.Equal(27, accountItem.Slot);
		var cubeItem = Assert.Single(player.InventoryItems);
		Assert.Equal(6001, cubeItem.ObjectId);
		Assert.Equal(1001, cubeItem.OwnerId);
		Assert.Equal(0, cubeItem.Location);
		Assert.Equal(12, cubeItem.Slot);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Equal(1, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Equal((1001, 77, 0, 5001, 0, 2, 27L, 6001, 2, 0, 12L), repository.SavedItemStorageSwitchMutation);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 2,
				expectedObjectId: 6001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 6001,
				expectedItemId: 201,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 1,
				expectedSlot: 27),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0, expectedStorage: 2));
	}

	[Fact]
	public async Task ProcessPacketAsync_CubeAndLegionWarehouseReplaceSwitchesOwnersLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 1, accountId: 77);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 201,
					Count = 2,
					OwnerId = 88,
					Location = 3,
					Slot = 27,
				},
			])
			.ToArray();
		player.LegionId = 88;
		player.LegionRank = "VOLUNTEER";
		player.LegionVolunteerPermission = 0x4;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(178, buffer =>
			{
				buffer.WriteC(0);
				buffer.WriteD(5001);
				buffer.WriteC(3);
				buffer.WriteD(6001);
			}));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(88, item.OwnerId);
				Assert.Equal(3, item.Location);
				Assert.Equal(27, item.Slot);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(1001, item.OwnerId);
				Assert.Equal(0, item.Location);
				Assert.Equal(12, item.Slot);
			});
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Equal(1, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Equal((1001, 77, 88, 5001, 0, 3, 27L, 6001, 3, 0, 12L), repository.SavedItemStorageSwitchMutation);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteWarehouseItemPayload(
				Assert.IsType<SmDeleteWarehouseItem>(packet),
				expectedWarehouseType: 3,
				expectedObjectId: 6001,
				expectedDeleteType: SmDeleteItem.MoveDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 3),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 6001,
				expectedItemId: 201,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 3,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 1,
				expectedSlot: 27),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 3));
	}

	[Fact]
	public async Task HandleReplaceItemAsync_RegularWarehouseRestrictionSendsDenialAndUnlocksBothLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 999900202, count: 1);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		var replaceItem = new InventoryItem
		{
			ObjectId = 6001,
			ItemId = 200,
			Count = 2,
			Location = 1,
			Slot = 27,
		};
		player.InventoryItems = player.InventoryItems.Concat([replaceItem]).ToArray();

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 0, sourceItemObjectId: 5001, replaceStorageType: 1, replaceItemObjectId: 6001));

		Assert.Equal(0, sourceItem.Location);
		Assert.Equal(12, sourceItem.Slot);
		Assert.Equal(1, replaceItem.Location);
		Assert.Equal(27, replaceItem.Slot);
		Assert.Equal(0, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300418),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 999900202,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 6001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedItemId: 200,
				expectedCount: 2,
				expectedSlot: 27),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1));
	}

	[Fact]
	public async Task HandleReplaceItemAsync_ShutdownSoonUnlocksBothItemsLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, isShuttingDownSoon: () => true);
		var player = CreatePlayer(itemId: 200, count: 1);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		var replaceItem = new InventoryItem
		{
			ObjectId = 6001,
			ItemId = 201,
			Count = 2,
			Location = 1,
			Slot = 27,
		};
		player.InventoryItems = player.InventoryItems.Concat([replaceItem]).ToArray();

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 0, sourceItemObjectId: 5001, replaceStorageType: 1, replaceItemObjectId: 6001));

		Assert.Equal(0, sourceItem.Location);
		Assert.Equal(12, sourceItem.Slot);
		Assert.Equal(1, replaceItem.Location);
		Assert.Equal(27, replaceItem.Slot);
		Assert.Equal(0, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 5001,
				expectedItemId: 200,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedSlot: 12),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 6001,
				expectedWarehouseType: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedItemId: 201,
				expectedCount: 2,
				expectedSlot: 27),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 1),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1390230, "Shutdown Progress"));
	}

	[Fact]
	public async Task HandleReplaceItemAsync_StorageSwitchSaveFailureRollsBackBothItems()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveItemStorageSwitchMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 1);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		var replaceItem = new InventoryItem
		{
			ObjectId = 6001,
			ItemId = 201,
			Count = 2,
			Location = 1,
			Slot = 27,
		};
		player.InventoryItems = player.InventoryItems.Concat([replaceItem]).ToArray();

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 0, sourceItemObjectId: 5001, replaceStorageType: 1, replaceItemObjectId: 6001));

		Assert.Equal(0, sourceItem.Location);
		Assert.Equal(12, sourceItem.Slot);
		Assert.Equal(1, replaceItem.Location);
		Assert.Equal(27, replaceItem.Slot);
		Assert.Equal(1, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleReplaceItemAsync_AccountWarehouseSaveFailureRollsBackListsAndOwners()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveItemStorageSwitchMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 1, accountId: 77);
		var sourceItem = Assert.Single(player.InventoryItems);
		sourceItem.Slot = 12;
		var replaceItem = new InventoryItem
		{
			ObjectId = 6001,
			ItemId = 201,
			Count = 2,
			OwnerId = 77,
			Location = 2,
			Slot = 27,
		};
		player.AccountWarehouseItems = [replaceItem];

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 0, sourceItemObjectId: 5001, replaceStorageType: 2, replaceItemObjectId: 6001));

		var cubeItem = Assert.Single(player.InventoryItems);
		Assert.Same(sourceItem, cubeItem);
		Assert.Equal(1001, sourceItem.OwnerId);
		Assert.Equal(0, sourceItem.Location);
		Assert.Equal(12, sourceItem.Slot);
		var accountItem = Assert.Single(player.AccountWarehouseItems);
		Assert.Same(replaceItem, accountItem);
		Assert.Equal(77, replaceItem.OwnerId);
		Assert.Equal(2, replaceItem.Location);
		Assert.Equal(27, replaceItem.Slot);
		Assert.Equal(1, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Equal((1001, 77, 0, 5001, 0, 2, 27L, 6001, 2, 0, 12L), repository.SavedItemStorageSwitchMutation);
		Assert.Equal(0, repository.SaveItemCrossStorageMoveMutationCalls);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleReplaceItemAsync_AccountWarehouseSameStorageSwitchPersistsSlotsWithAccountOwnerLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId: 200, count: 0, accountId: 77);
		player.InventoryItems = [];
		var sourceItem = new InventoryItem
		{
			ObjectId = 5001,
			ItemId = 200,
			Count = 1,
			OwnerId = 77,
			Location = 2,
			Slot = 12,
		};
		var replaceItem = new InventoryItem
		{
			ObjectId = 6001,
			ItemId = 200,
			Count = 2,
			OwnerId = 77,
			Location = 2,
			Slot = 27,
		};
		player.AccountWarehouseItems = [sourceItem, replaceItem];

		await InvokeHandleReplaceItemAsync(
			fixture.Connection,
			player,
			CreateReplaceItem(sourceStorageType: 2, sourceItemObjectId: 5001, replaceStorageType: 2, replaceItemObjectId: 6001));

		Assert.Equal(27, sourceItem.Slot);
		Assert.Equal(12, replaceItem.Slot);
		Assert.Equal(2, player.AccountWarehouseItems.Count);
		Assert.Empty(player.InventoryItems);
		Assert.Equal(2, repository.SaveInventoryItemSlotCalls);
		Assert.Equal(
			[(77, 5001, 27L), (77, 6001, 12L)],
			repository.SavedInventoryItemSlots);
		Assert.Equal(0, repository.SaveItemStorageSwitchMutationCalls);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleChargeItemAsync_SaveFailureStopsBeforeInMemoryMutationAndPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveItemChargeMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargePaymentPlayer();

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItem(itemObjectId: 7001, chargeLevel: 1));

		Assert.Equal(1000, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeMutationCalls);
		Assert.Equal(500, repository.ChargePaymentAbyssRank?.Ap);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7001);
		Assert.Equal(0, item.Charge);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleChargeItemAsync_KinahPaymentRejectsInsufficientKinahWithoutSideEffects()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateKinahChargePaymentPlayer(kinah: 499);

		await InvokeHandleChargeItemAsync(fixture.Connection, player, CreateChargeItem(itemObjectId: 7101, chargeLevel: 1));

		Assert.Equal(0, repository.SaveItemChargeMutationCalls);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7101);
		var kinah = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 182400001);
		Assert.Equal(0, item.Charge);
		Assert.Equal(499, kinah.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentSendsAbyssPointsPlannerPackets()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(new EmptyPlayerEnterWorldRepository());
		var player = CreateChargeAllPaymentPlayer();
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(500, player.AbyssRank.Ap);
		Assert.Null(player.PendingChargeAllRequest);
		var chargedItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 7001);
		Assert.Equal(ItemChargeService.Level1ChargePoints, chargedItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "500"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentSendsPerItemUpdatesStatsThenAllComplete()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayerWithTwoItems();
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 1_000,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
				new PendingChargeAllItem(
					ObjectId: 7002,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(0, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(0, repository.ChargeAllPaymentAbyssRank?.Ap);
		Assert.Equal([7001, 7002], repository.ChargeAllChargedItems.Select(item => item.ObjectId));
		Assert.Null(player.PendingChargeAllRequest);
		Assert.Collection(
			player.InventoryItems.OrderBy(inventoryItem => inventoryItem.ObjectId).Where(inventoryItem => inventoryItem.ItemId == 100000400),
			first => Assert.Equal(ItemChargeService.Level1ChargePoints, first.Charge),
			second => Assert.Equal(ItemChargeService.Level1ChargePoints, second.Charge));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "1000"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7001),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401335, "Test Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7002),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401335, "Test Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401340));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentHonorsConfiguredAbyssPointCapClamp()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			options: CreateApCapOptions());
		var player = CreateChargeAllPaymentPlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 1_600, Rank = 2, MaxRank = 2 };
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(1_000, player.AbyssRank.Ap);
		Assert.Equal(1_000, repository.ChargeAllPaymentAbyssRank?.Ap);
		Assert.Null(player.PendingChargeAllRequest);
		var chargedItem = Assert.Single(player.InventoryItems, item => item.ObjectId == 7001);
		Assert.Equal(ItemChargeService.Level1ChargePoints, chargedItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "600"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentSaveFailureStopsBeforeInMemoryMutationAndPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveItemChargeAllMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayer();
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(1000, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(500, repository.ChargeAllPaymentAbyssRank?.Ap);
		var stagedItem = Assert.Single(repository.ChargeAllChargedItems);
		Assert.Equal(7001, stagedItem.ObjectId);
		Assert.Equal(ItemChargeService.Level1ChargePoints, stagedItem.Charge);
		Assert.Null(player.PendingChargeAllRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7001);
		Assert.Equal(0, item.Charge);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentRejectsInsufficientAbyssPointsWithoutSideEffects()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 499 };
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(499, player.AbyssRank.Ap);
		Assert.Null(player.PendingChargeAllRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Null(repository.ChargeAllPaymentAbyssRank);
		Assert.Equal(0, repository.SaveItemChargeAllMutationCalls);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7001);
		Assert.Equal(0, item.Charge);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentStillSpendsWhenItemAlreadyChargedAtAccept()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayer(charge: ItemChargeService.Level1ChargePoints);
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(500, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(500, repository.ChargeAllPaymentAbyssRank?.Ap);
		Assert.Empty(repository.ChargeAllChargedItems);
		Assert.Null(player.PendingChargeAllRequest);
		var unchangedItem = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7001);
		Assert.Equal(ItemChargeService.Level1ChargePoints, unchangedItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "500"),
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentChargesOnlyCurrentChargeableItemWhenOnePendingItemIsStale()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayerWithTwoItems(firstCharge: ItemChargeService.Level1ChargePoints, secondCharge: 0);
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 1_000,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
				new PendingChargeAllItem(
					ObjectId: 7002,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(0, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(0, repository.ChargeAllPaymentAbyssRank?.Ap);
		var chargedItem = Assert.Single(repository.ChargeAllChargedItems);
		Assert.Equal(7002, chargedItem.ObjectId);
		Assert.Null(player.PendingChargeAllRequest);
		Assert.Collection(
			player.InventoryItems.OrderBy(inventoryItem => inventoryItem.ObjectId).Where(inventoryItem => inventoryItem.ItemId == 100000400),
			first => Assert.Equal(ItemChargeService.Level1ChargePoints, first.Charge),
			second => Assert.Equal(ItemChargeService.Level1ChargePoints, second.Charge));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "1000"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7002),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401340));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllApPaymentChargesOnlyCurrentChargeableItemWhenOnePendingItemIsMissing()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateChargeAllPaymentPlayerWithTwoItems();
		player.InventoryItems = player.InventoryItems.Where(inventoryItem => inventoryItem.ObjectId != 7001).ToArray();
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 2,
			PaymentAmount: 1_000,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7001,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
				new PendingChargeAllItem(
					ObjectId: 7002,
					ItemId: 100000400,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Equal(0, player.AbyssRank.Ap);
		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(0, repository.ChargeAllPaymentAbyssRank?.Ap);
		var chargedItem = Assert.Single(repository.ChargeAllChargedItems);
		Assert.Equal(7002, chargedItem.ObjectId);
		Assert.Null(player.PendingChargeAllRequest);
		var remainingItem = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 100000400);
		Assert.Equal(7002, remainingItem.ObjectId);
		Assert.Equal(ItemChargeService.Level1ChargePoints, remainingItem.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300965, "1000"),
			packet => Assert.IsType<SmAbyssRank>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7002),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1401340));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllKinahPaymentSendsPerItemUpdatesStatsThenAllComplete()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateKinahChargeAllPaymentPlayerWithTwoItems(kinah: 1_000);
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 1,
			PaymentAmount: 1_000,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7101,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
				new PendingChargeAllItem(
					ObjectId: 7102,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemChargeAllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemChargeAllConfirm, response: 1));

		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(0, repository.ChargeAllPaymentKinahItem?.Count);
		Assert.Null(repository.ChargeAllPaymentAbyssRank);
		Assert.Equal([7101, 7102], repository.ChargeAllChargedItems.Select(item => item.ObjectId));
		Assert.Null(player.PendingChargeAllRequest);
		var kinah = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 182400001);
		Assert.Equal(0, kinah.Count);
		Assert.Collection(
			player.InventoryItems.OrderBy(inventoryItem => inventoryItem.ObjectId).Where(inventoryItem => inventoryItem.ItemId == 100000401),
			first => Assert.Equal(ItemChargeService.Level1ChargePoints, first.Charge),
			second => Assert.Equal(ItemChargeService.Level1ChargePoints, second.Charge));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 8101, expectedUpdateType: SmInventoryUpdateItem.DecreaseKinahBuy),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7101),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400887, "Test Kinah Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7102),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400887, "Test Kinah Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400892));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllKinahPaymentRejectsInsufficientKinahWithoutSideEffects()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateKinahChargeAllPaymentPlayer(kinah: 499);
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 1,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7101,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemChargeAllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemChargeAllConfirm, response: 1));

		Assert.Null(player.PendingChargeAllRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Equal(0, repository.SaveItemChargeAllMutationCalls);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7101);
		var kinah = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 182400001);
		Assert.Equal(0, item.Charge);
		Assert.Equal(499, kinah.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllKinahPaymentSaveFailureStopsBeforeInMemoryMutationAndPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveItemChargeAllMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateKinahChargeAllPaymentPlayer(kinah: 500);
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 1,
			PaymentAmount: 500,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7101,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemChargeAllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemChargeAllConfirm, response: 1));

		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Equal(0, repository.ChargeAllPaymentKinahItem?.Count);
		Assert.Null(repository.ChargeAllPaymentAbyssRank);
		var stagedItem = Assert.Single(repository.ChargeAllChargedItems);
		Assert.Equal(7101, stagedItem.ObjectId);
		Assert.Equal(ItemChargeService.Level1ChargePoints, stagedItem.Charge);
		Assert.Null(player.PendingChargeAllRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ObjectId == 7101);
		var kinah = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 182400001);
		Assert.Equal(0, item.Charge);
		Assert.Equal(500, kinah.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllKinahPaymentChargesOnlyCurrentChargeableItemWhenOnePendingItemIsStale()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateKinahChargeAllPaymentPlayerWithTwoItems(
			kinah: 1_000,
			firstCharge: ItemChargeService.Level1ChargePoints,
			secondCharge: 0);
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 1,
			PaymentAmount: 1_000,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7101,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
				new PendingChargeAllItem(
					ObjectId: 7102,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemChargeAllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemChargeAllConfirm, response: 1));

		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Null(repository.ChargeAllPaymentAbyssRank);
		var repositoryChargedItem = Assert.Single(repository.ChargeAllChargedItems);
		Assert.Equal(7102, repositoryChargedItem.ObjectId);
		Assert.Null(player.PendingChargeAllRequest);
		var kinah = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 182400001);
		Assert.Equal(0, kinah.Count);
		Assert.Collection(
			player.InventoryItems.OrderBy(inventoryItem => inventoryItem.ObjectId).Where(inventoryItem => inventoryItem.ItemId == 100000401),
			first => Assert.Equal(ItemChargeService.Level1ChargePoints, first.Charge),
			second => Assert.Equal(ItemChargeService.Level1ChargePoints, second.Charge));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 8101, expectedUpdateType: SmInventoryUpdateItem.DecreaseKinahBuy),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7102),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400887, "Test Kinah Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400892));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllKinahPaymentChargesOnlyCurrentChargeableItemWhenOnePendingItemIsMissing()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreateKinahChargeAllPaymentPlayerWithTwoItems(kinah: 1_000);
		player.InventoryItems = player.InventoryItems
			.Where(inventoryItem => inventoryItem.ObjectId != 7101)
			.ToArray();
		var pendingRequest = new PendingChargeAllRequest(
			SenderObjectId: player.ObjectId,
			ChargeWay: 1,
			PaymentAmount: 1_000,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 7101,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
				new PendingChargeAllItem(
					ObjectId: 7102,
					ItemId: 100000401,
					PreviousCharge: 0,
					TargetCharge: ItemChargeService.Level1ChargePoints,
					Level: 1),
			]);
		player.PendingChargeAllRequest = pendingRequest;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.ItemChargeAllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pendingRequest)));

		await fixture.Connection.HandleQuestionResponseAsync(player, CreateQuestionResponse(SmQuestionWindow.ItemChargeAllConfirm, response: 1));

		Assert.Equal(1, repository.SaveItemChargeAllMutationCalls);
		Assert.Null(repository.ChargeAllPaymentAbyssRank);
		var repositoryChargedItem = Assert.Single(repository.ChargeAllChargedItems);
		Assert.Equal(7102, repositoryChargedItem.ObjectId);
		Assert.Null(player.PendingChargeAllRequest);
		var kinah = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 182400001);
		Assert.Equal(0, kinah.Count);
		var item = Assert.Single(player.InventoryItems, inventoryItem => inventoryItem.ItemId == 100000401);
		Assert.Equal(7102, item.ObjectId);
		Assert.Equal(ItemChargeService.Level1ChargePoints, item.Charge);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 8101, expectedUpdateType: SmInventoryUpdateItem.DecreaseKinahBuy),
			packet => AssertChargeInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 7102),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400887, "Test Kinah Conditioning Sword", "1"),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1400892));
	}

	[Fact]
	public async Task HandleUseItemAsync_AnimationAddSchedulesPositiveTimeUseAndClearsUsingItem()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 188500000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(5001, player.UsingItemObjectId);
		var startAnimation = Assert.Single(fixture.SentPackets.OfType<SmItemUsageAnimation>());
		using (var reader = new PacketBuffer(SerializeUnencryptedPayload(startAnimation)))
		{
			Assert.Equal(1001, reader.ReadD());
			Assert.Equal(1001, reader.ReadD());
			Assert.Equal(5001, reader.ReadD());
			Assert.Equal(188500000, reader.ReadD());
			Assert.Equal(1000, reader.ReadD());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(1, (int)reader.ReadC());
			Assert.Equal(0, reader.ReadD());
			Assert.Equal(0, reader.Remaining);
		}

		await WaitUntilAsync(() => player.UsingItemObjectId == 0);
	}

	[Fact]
	public async Task HandleEmotionAsync_AnimationAddPendingUseCancelsAndSendsEndState()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 188500000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));
		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.SelectTarget));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedTime: 1000, expectedEnd: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedTime: 0, expectedEnd: 3),
			packet => Assert.IsType<SmSystemMessage>(packet));
		await Task.Delay(1100);
		Assert.Equal(3, fixture.SentPackets.Count);
	}

	[Fact]
	public async Task HandleEmotionAsync_DecomposePendingUseCancelsAndSendsEndState()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 100);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));
		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.SelectTarget));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 2),
			packet => Assert.IsType<SmSystemMessage>(packet));
		await Task.Delay(3100);
		Assert.Equal(3, fixture.SentPackets.Count);
	}

	[Fact]
	public async Task HandleEmotionAsync_StanceCancelsCurrentCastBeforeModeGuardMessage()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 100);
		player.StanceSkillId = 1234;
		player.SetCastingSkill(7001);

		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.Sit));

		Assert.Equal(0, player.CastingSkillId);
		Assert.Equal(7001, player.LastCastingSkillId);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSkillCancelPayload(Assert.IsType<SmSkillCancel>(packet), player.ObjectId, 7001),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300023),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300124));
		Assert.False(player.IsInState(PlayerCreatureState.Resting));
	}

	[Fact]
	public async Task HandleEmotionAsync_StanceFlySendsTakeoffGuardMessage()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 100);
		player.StanceSkillId = 1234;

		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.Fly));

		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300147));
		Assert.False(player.IsFlying());
	}

	[Fact]
	public async Task HandleEmotionAsync_ItemSkillCastCancelsCooldownAndUsageBeforeModeChange()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 100);
		player.AddItemCooldown(77, 5_000, new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero));
		player.SetCastingSkill(
			9001,
			PlayerCastingSkillMethod.Item,
			itemObjectId: 5001,
			itemTemplateId: 100,
			firstTargetObjectId: player.ObjectId,
			itemCooldownDelayId: 77);

		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.Sit));

		Assert.Equal(0, player.CastingSkillId);
		Assert.Equal(9001, player.LastCastingSkillId);
		Assert.False(player.ItemCooldowns.ContainsKey(77));
		Assert.True(player.IsInState(PlayerCreatureState.Resting));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300427),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 3),
			packet => Assert.IsType<SmEmotion>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeCompletesAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(5001, player.UsingItemObjectId);
		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(5));
		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(100, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(200, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 200, expectedCount: 1),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeDeletesLastSourceAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100, count: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(5001, player.UsingItemObjectId);
		await WaitUntilAsync(() => fixture.SentPackets.Count >= 7, TimeSpan.FromSeconds(5));
		Assert.Equal(0, player.UsingItemObjectId);
		var reward = Assert.Single(player.InventoryItems);
		Assert.Equal(200, reward.ItemId);
		Assert.Equal(1, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 200, expectedCount: 1),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposePersistenceFailureDoesNotMutateRuntimeInventory()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveDecomposeActionMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => repository.SaveDecomposeActionMutationCalls == 1, TimeSpan.FromSeconds(5));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5001 && item.ItemId == 100 && item.Count == 2);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 200);
		var packet = Assert.Single(fixture.SentPackets);
		AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0);
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeAddsRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(5));
		Assert.Contains(player.InventoryItems, item => item.ItemId == 200 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertDecomposableAddPayloadWithCleanupSealFlag(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 200, expectedCount: 1, expectedCleanupSealFlag: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeMergesRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6001]));
		var player = CreatePlayer(itemId: 100);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 200,
					Count = 1,
					Location = 0,
				},
			])
			.ToArray();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 5, TimeSpan.FromSeconds(5));
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 200);
		Assert.Equal(2, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 6001, expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect, expectedCleanupSealFlag: 3),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_AssemblyAddsRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 5100, 5101]));
		var player = CreateAssemblyPlayer();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 7, TimeSpan.FromSeconds(5));
		Assert.Contains(player.InventoryItems, item => item.ItemId == 188053996 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 103, expectedTime: 1000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5100,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5101,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 103, expectedTime: 0, expectedEnd: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryItemCollectAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 188053996, expectedCount: 1, expectedCleanupSealFlag: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 4));
	}

	[Fact]
	public async Task HandleUseItemAsync_AssemblyMergesRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 5100, 5101, 6001]));
		var player = CreateAssemblyPlayer(existingRewardCount: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(5));
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 188053996);
		Assert.Equal(2, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 103, expectedTime: 1000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5100,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5101,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 103, expectedTime: 0, expectedEnd: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 6001, expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect, expectedCleanupSealFlag: 3));
	}

	[Fact]
	public async Task HandleUseItemAsync_AssemblyKeepsEarlierPartConsumeWhenLaterPartDisappearsBeforeCompletion()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 5100, 5101]));
		var player = CreateAssemblyPlayer();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));
		player.InventoryItems = player.InventoryItems.Where(item => item.ObjectId != 5101).ToArray();

		await WaitUntilAsync(() => repository.SaveAssemblyItemActionMutationCalls == 1, TimeSpan.FromSeconds(5));

		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 5101);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5100 && item.Count == 1);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 188053996);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 103, expectedTime: 1000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5100,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExpExtractAddsRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreateExpExtractPlayer();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(6));
		Assert.Equal(140, player.Exp);
		Assert.Contains(player.InventoryItems, item => item.ItemId == 188053996 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 104, expectedTime: 5000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet => AssertInventoryItemCollectAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 188053996, expectedCount: 1, expectedCleanupSealFlag: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 104, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExpExtractMergesRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6001]));
		var player = CreateExpExtractPlayer(existingRewardCount: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(6));
		Assert.Equal(140, player.Exp);
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 188053996);
		Assert.Equal(2, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 104, expectedTime: 5000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => Assert.IsType<SmStatUpdateExp>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 6001, expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect, expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 104, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExtractAddsRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6200]));
		var player = CreateExtractPlayer();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6200));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(6));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 6200);
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 166000195);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 5000, expectedEnd: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6200, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryItemCollectAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 166000195, expectedCount: reward.Count, expectedCleanupSealFlag: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExtractMergesRestrictedRewardWithCleanupSealFlag()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6200, 6001]));
		var player = CreateExtractPlayer(existingRewardCount: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6200));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(6));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 6200);
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 166000195);
		Assert.True(reward.Count >= 3);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 5000, expectedEnd: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6200, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 6001, expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect, expectedCleanupSealFlag: 3),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExtractDeletesLastSourceWithUseDeleteAndCubeUpdate()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6200]));
		var player = CreateExtractPlayer(sourceCount: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6200));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 8, TimeSpan.FromSeconds(6));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId is 5001 or 6200);
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 166000195);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 5000, expectedEnd: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6200, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryItemCollectAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 166000195, expectedCount: reward.Count, expectedCleanupSealFlag: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExtractInventoryFullStillConsumesAndSendsDiceError()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6200]),
			extractionRewardMaxStackCount: 1);
		var player = CreateExtractPlayer();
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 25).Select(index => new InventoryItem
			{
				ObjectId = 7000 + index,
				ItemId = 200,
				Count = 1,
				Location = 0,
			}))
			.ToArray();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6200));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 8, TimeSpan.FromSeconds(6));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 6200);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5001 && item.Count == 1);
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 166000195);
		Assert.Equal(1, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 5000, expectedEnd: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6200, expectedDeleteType: 0),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 26),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryItemCollectAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 166000195, expectedCount: 1, expectedCleanupSealFlag: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 27),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1390182),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task HandleUseItemAsync_ExtractMissingRewardTemplateFailsWithoutMutation()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6200]),
			includeExtractionRewardTemplate: false);
		var player = CreateExtractPlayer();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6200));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 2, TimeSpan.FromSeconds(6));
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5001 && item.Count == 2);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 6200);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 166000195);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 5000, expectedEnd: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 0, expectedEnd: 2));
	}

	[Theory]
	[InlineData(5001)]
	[InlineData(6200)]
	public async Task HandleUseItemAsync_ExtractMissingScheduledItemSendsFailureWithoutMutation(int missingObjectId)
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001, 6200]));
		var player = CreateExtractPlayer();
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItemTarget(sourceItemObjectId: 5001, targetItemObjectId: 6200));
		player.InventoryItems = player.InventoryItems
			.Where(item => item.ObjectId != missingObjectId)
			.ToArray();

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 2, TimeSpan.FromSeconds(6));
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 166000195);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == missingObjectId);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == (missingObjectId == 5001 ? 6200 : 5001));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 5000, expectedEnd: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 105, expectedTime: 0, expectedEnd: 2));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeInventoryFullDoesNotScheduleOrMutate()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 100);
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 26).Select(index => new InventoryItem
			{
				ObjectId = 6000 + index,
				ItemId = 201,
				Count = 1,
				Location = 0,
			}))
			.ToArray();
		var originalInventory = player.InventoryItems
			.OrderBy(item => item.ObjectId)
			.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
			.ToArray();

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		Assert.Equal(
			originalInventory,
			player.InventoryItems
				.OrderBy(item => item.ObjectId)
				.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
				.ToArray());
		var packet = Assert.Single(fixture.SentPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300447);
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeInventoryFullBeforeCompletionFailsWithoutMutation()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			repository,
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 26).Select(index => new InventoryItem
			{
				ObjectId = 6100 + index,
				ItemId = 201,
				Count = 1,
				Location = 0,
			}))
			.ToArray();

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 3, TimeSpan.FromSeconds(5));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 5001 && item.ItemId == 100 && item.Count == 2);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 200);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300447),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 2));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeSpecialCubeFullDoesNotScheduleOrMutate()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 102);
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 102).Select(index => new InventoryItem
			{
				ObjectId = 7000 + index,
				ItemId = 205,
				Count = 1,
				Location = 0,
			}))
			.ToArray();
		var originalInventory = player.InventoryItems
			.OrderBy(item => item.ObjectId)
			.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
			.ToArray();

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		Assert.Equal(
			originalInventory,
			player.InventoryItems
				.OrderBy(item => item.ObjectId)
				.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
				.ToArray());
		var packet = Assert.Single(fixture.SentPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300447);
	}

	[Fact]
	public async Task HandleUseItemAsync_SelectableDecomposeShowsChoicesWithoutSchedulingUse()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 101);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(0, player.UsingItemObjectId);
		var packet = Assert.Single(fixture.SentPackets);
		AssertFirstShowDecomposablePayload(Assert.IsType<SmFirstShowDecomposable>(packet));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_SelectableRewardConsumesSourceAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 1));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(202, item.ItemId);
				Assert.Equal(3, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 202, expectedCount: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_FullCubeStillAddsSelectableRewardLikeJavaOverflow()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 26).Select(index => new InventoryItem
			{
				ObjectId = 6000 + index,
				ItemId = 200,
				Count = 1,
				Location = 0,
			}))
			.ToArray();

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 1));

		Assert.Equal(1, repository.SaveDecomposeActionMutationCalls);
		Assert.Equal(28, InventoryCapacity.GetUsedCubeSlots(player));
		Assert.Contains(player.InventoryItems, item => item is { ObjectId: 5001, ItemId: 101, Count: 1 });
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 202);
		Assert.Equal(3, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 202, expectedCount: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 28));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(99)]
	public async Task HandleSelectDecomposableAsync_InvalidSelectableRewardIndexDoesNotMutateInventory(int index)
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index));

		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_SelectableRewardDeletesSingleCountSource()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101, count: 1);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(201, rewardItem.ItemId);
		Assert.Equal(2, rewardItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmDeleteItem>(packet),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 201, expectedCount: 2),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task CaptureSelectableDecomposeObservationJson_ProjectsContractComparablePackets()
	{
		var decrementJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEC-001", sourceCount: 2, selectIndex: 1);
		var deleteJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEL-001", sourceCount: 1, selectIndex: 0);

		using var decrement = JsonDocument.Parse(decrementJson);
		Assert.Equal("JD-SEL-DEC-001", decrement.RootElement.GetProperty("scenario_id").GetString());
		Assert.Equal(
			[
				"SM_ITEM_USAGE_ANIMATION",
				"SM_SYSTEM_MESSAGE",
				"SM_INVENTORY_UPDATE_ITEM",
				"SM_SECONDARY_SHOW_DECOMPOSABLE",
				"SM_INVENTORY_ADD_ITEM",
				"SM_CUBE_UPDATE",
			],
			ReadPacketClasses(decrement.RootElement));
		Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, GetPacket(decrement.RootElement, 3).GetProperty("decoded_fields").GetProperty("update_type_mask").GetInt32());
		Assert.Equal(
			"STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED",
			GetPacket(decrement.RootElement, 2).GetProperty("decoded_fields").GetProperty("factory_name").GetString());
		Assert.Equal(202, GetPacket(decrement.RootElement, 5).GetProperty("decoded_fields").GetProperty("item_id").GetInt32());
		Assert.Equal(3, GetPacket(decrement.RootElement, 5).GetProperty("decoded_fields").GetProperty("count").GetInt64());
		Assert.Equal(2, GetPacket(decrement.RootElement, 6).GetProperty("decoded_fields").GetProperty("items_count").GetInt32());

		using var delete = JsonDocument.Parse(deleteJson);
		Assert.Equal("JD-SEL-DEL-001", delete.RootElement.GetProperty("scenario_id").GetString());
		Assert.Equal(
			[
				"SM_ITEM_USAGE_ANIMATION",
				"SM_SYSTEM_MESSAGE",
				"SM_DELETE_ITEM",
				"SM_CUBE_UPDATE",
				"SM_SECONDARY_SHOW_DECOMPOSABLE",
				"SM_INVENTORY_ADD_ITEM",
				"SM_CUBE_UPDATE",
			],
			ReadPacketClasses(delete.RootElement));
		Assert.Equal(SmDeleteItem.UseDeleteType, GetPacket(delete.RootElement, 3).GetProperty("decoded_fields").GetProperty("delete_type").GetInt32());
		Assert.Equal(
			"STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED",
			GetPacket(delete.RootElement, 2).GetProperty("decoded_fields").GetProperty("factory_name").GetString());
		Assert.Equal(0, GetPacket(delete.RootElement, 4).GetProperty("decoded_fields").GetProperty("items_count").GetInt32());
		Assert.Equal(201, GetPacket(delete.RootElement, 6).GetProperty("decoded_fields").GetProperty("item_id").GetInt32());
		Assert.Equal(2, GetPacket(delete.RootElement, 6).GetProperty("decoded_fields").GetProperty("count").GetInt64());
		Assert.Equal(1, GetPacket(delete.RootElement, 7).GetProperty("decoded_fields").GetProperty("items_count").GetInt32());
	}

	[Fact]
	public async Task CaptureSelectableDecomposeObservationJson_WithRealJavaXmlCandidate_ProjectsRealRewardCounts()
	{
		var candidate = SelectableDecomposeTestData.RealJavaXmlCandidate;
		var decrementJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEC-001", sourceCount: 2, selectIndex: 1, candidate);
		var deleteJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEL-001", sourceCount: 1, selectIndex: 0, candidate);

		using var decrement = JsonDocument.Parse(decrementJson);
		Assert.Equal(candidate.SourceItemId, GetPacket(decrement.RootElement, 1).GetProperty("decoded_fields").GetProperty("item_id").GetInt32());
		Assert.Equal(candidate.RewardIndex1ItemId, GetPacket(decrement.RootElement, 5).GetProperty("decoded_fields").GetProperty("item_id").GetInt32());
		Assert.Equal(candidate.RewardIndex1Count, GetPacket(decrement.RootElement, 5).GetProperty("decoded_fields").GetProperty("count").GetInt64());

		using var delete = JsonDocument.Parse(deleteJson);
		Assert.Equal(candidate.SourceItemId, GetPacket(delete.RootElement, 1).GetProperty("decoded_fields").GetProperty("item_id").GetInt32());
		Assert.Equal(candidate.RewardIndex0ItemId, GetPacket(delete.RootElement, 6).GetProperty("decoded_fields").GetProperty("item_id").GetInt32());
		Assert.Equal(candidate.RewardIndex0Count, GetPacket(delete.RootElement, 6).GetProperty("decoded_fields").GetProperty("count").GetInt64());
	}

	[Fact]
	public async Task CompareSelectableDecomposeJavaArtifacts_WhenPresent_ComparesContractFields()
	{
		var artifactRoot = Path.Combine(FindRepositoryRoot(), "docs", "parity-artifacts", "java", "decompose", "selectable");
		var scenarios = new[]
		{
			new SelectableDecomposeArtifactScenario("JD-SEL-DEC-001", SourceCount: 2, SelectIndex: 1),
			new SelectableDecomposeArtifactScenario("JD-SEL-DEL-001", SourceCount: 1, SelectIndex: 0),
		};
		var missingArtifacts = scenarios
			.Select(scenario => Path.Combine(artifactRoot, scenario.ScenarioId + ".json"))
			.Where(path => !File.Exists(path))
			.ToArray();
		if (missingArtifacts.Length > 0)
		{
			_output.WriteLine("Needs Verification: Java selectable-decompose artifacts are not present yet.");
			foreach (var missingArtifact in missingArtifacts)
				_output.WriteLine("Missing Java artifact: " + missingArtifact);
			return;
		}

		foreach (var scenario in scenarios)
		{
			var javaJson = await File.ReadAllTextAsync(Path.Combine(artifactRoot, scenario.ScenarioId + ".json"));
			var csharpJson = await CaptureSelectableDecomposeObservationJsonAsync(scenario.ScenarioId, scenario.SourceCount, scenario.SelectIndex);
			using var javaObservation = JsonDocument.Parse(javaJson);
			using var csharpObservation = JsonDocument.Parse(csharpJson);
			AssertSelectableDecomposeObservationMatchesJavaArtifact(javaObservation.RootElement, csharpObservation.RootElement);
		}
	}

	[Fact]
	public async Task CompareSelectableDecomposeJavaArtifacts_WithDeclaredIdMapping_NormalizesMappedIds()
	{
		var csharpJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEC-001", sourceCount: 2, selectIndex: 1);
		var javaJson = CreateMappedSelectableDecomposeJavaArtifactJson(
			csharpJson,
			sourceItemId: 188052590,
			rewardItemId: 188052592);

		using var javaObservation = JsonDocument.Parse(javaJson);
		using var csharpObservation = JsonDocument.Parse(csharpJson);

		AssertSelectableDecomposeObservationMatchesJavaArtifact(javaObservation.RootElement, csharpObservation.RootElement);
	}

	[Fact]
	public async Task CompareSelectableDecomposeJavaArtifacts_WithDeclaredObjectIdMapping_NormalizesMappedObjectIds()
	{
		var csharpJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEC-001", sourceCount: 2, selectIndex: 1);
		var javaJson = CreateMappedSelectableDecomposeJavaArtifactJson(
			csharpJson,
			sourceItemId: 101,
			rewardItemId: 202,
			sourceObjectId: 70001,
			rewardObjectId: 70002);

		using var javaObservation = JsonDocument.Parse(javaJson);
		using var csharpObservation = JsonDocument.Parse(csharpJson);

		AssertSelectableDecomposeObservationMatchesJavaArtifact(javaObservation.RootElement, csharpObservation.RootElement);
	}

	[Fact]
	public async Task CompareSelectableDecomposeJavaArtifacts_WithRewardAddTrailingCubeUpdate_MatchesCurrentPacketShape()
	{
		var csharpJson = await CaptureSelectableDecomposeObservationJsonAsync("JD-SEL-DEC-001", sourceCount: 2, selectIndex: 1);
		var javaJson = CreateMappedSelectableDecomposeJavaArtifactJson(
			csharpJson,
			sourceItemId: 101,
			rewardItemId: 202);

		using var javaObservation = JsonDocument.Parse(javaJson);
		using var csharpObservation = JsonDocument.Parse(csharpJson);

		AssertSelectableDecomposeObservationMatchesJavaArtifact(javaObservation.RootElement, csharpObservation.RootElement);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_SelectableRewardMergesExistingStack()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001, 6001]));
		var player = CreatePlayer(itemId: 101);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 201,
					Count = 4,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(201, item.ItemId);
				Assert.Equal(6, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 6001, expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_PersistenceFailureDoesNotMutateRuntimeInventory()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveDecomposeActionMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001, 6001]));
		var player = CreatePlayer(itemId: 101);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 201,
					Count = 4,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Equal(1, repository.SaveDecomposeActionMutationCalls);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(101, item.ItemId);
				Assert.Equal(2, item.Count);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(201, item.ItemId);
				Assert.Equal(4, item.Count);
			});
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_MissingSourceDoesNotCallPersistenceOrSendPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 9999, index: 0));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_NonSelectableSourceDoesNotCallPersistenceOrSendPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(100, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_MissingRewardTemplateDoesNotCallPersistenceOrSendPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101, race: "ASMODIANS", playerClass: "GLADIATOR");

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 1));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Theory]
	[InlineData(true, 0)]
	[InlineData(false, 1)]
	public async Task HandleSelectDecomposableAsync_NonCubeOrEquippedSourceDoesNotCallPersistenceOrSendPackets(bool isEquipped, int location)
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101, isEquipped: isEquipped, location: location);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Equal(isEquipped, sourceItem.IsEquipped);
		Assert.Equal(location, sourceItem.Location);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_ReplaceItemFromLegionWarehouseWithoutWithdrawalSendsNoRightLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 7, location: 3);
		player.InventoryItems = player.InventoryItems.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 204,
					Count = 1,
					OwnerId = player.ObjectId,
					Location = 0,
					Slot = 4,
					PersistentState = InventoryItemPersistentState.Updated,
				},
			]).ToArray();
		player.LegionId = 77;
		player.LegionRank = "VOLUNTEER";
		player.LegionVolunteerPermission = 0x800;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(178, buffer =>
			{
				buffer.WriteC(3);
				buffer.WriteD(5001);
				buffer.WriteC(0);
				buffer.WriteD(6001);
			}));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(3, item.Location);
				Assert.Equal(7, item.Count);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(0, item.Location);
				Assert.Equal(1, item.Count);
				Assert.Equal(4, item.Slot);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300322),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 3,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedItemId: 200,
				expectedCount: 7),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 3),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 6001,
				expectedItemId: 204,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedSlot: 4),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_SplitItemFromLegionWarehouseWithoutWithdrawalSendsNoRightLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 7, location: 3);
		player.LegionId = 77;
		player.LegionRank = "VOLUNTEER";
		player.LegionVolunteerPermission = 0x800;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(157, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteQ(2L);
				buffer.WriteC(3);
				buffer.WriteD(0);
				buffer.WriteC(0);
				buffer.WriteH(-1);
			}));

		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(3, sourceItem.Location);
		Assert.Equal(7, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300322),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 3,
				expectedAddType: SmInventoryAddItem.ItemCollect,
				expectedItemId: 200,
				expectedCount: 7),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 3));
	}

	[Fact]
	public async Task ProcessPacketAsync_MoveItemFromLegionWarehouseWithoutWithdrawalSendsNoRightLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 200, count: 7, location: 3);
		player.LegionId = 77;
		player.LegionRank = "VOLUNTEER";
		player.LegionVolunteerPermission = 0x800;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(156, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteC(3);
				buffer.WriteC(0);
				buffer.WriteH(-1);
			}));

		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(3, sourceItem.Location);
		Assert.Equal(7, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300322),
			packet => AssertWarehouseAddPayload(
				Assert.IsType<SmWarehouseAddItem>(packet),
				expectedObjectId: 5001,
				expectedWarehouseType: 3,
				expectedAddType: SmInventoryAddItem.AllSlot,
				expectedItemId: 200,
				expectedCount: 7),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1, expectedStorage: 3));
	}

	[Fact]
	public async Task ProcessPacketAsync_LegionWarehouseKinahVolunteerWithdrawalSendsNoRightLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 182400001, count: 5000);
		player.LegionId = 77;
		player.LegionRank = "VOLUNTEER";
		player.LegionVolunteerPermission = 0x800;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var originalInventory = player.InventoryItems.Select(item => (item.ObjectId, item.Count)).ToArray();

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(76, buffer =>
			{
				buffer.WriteQ(1000L);
				buffer.WriteC(0);
			}));

		Assert.Equal(originalInventory, player.InventoryItems.Select(item => (item.ObjectId, item.Count)).ToArray());
		var packet = Assert.Single(fixture.SentPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300322);
	}

	[Fact]
	public async Task ProcessPacketAsync_LegionWarehouseKinahNoLegionReturnsWithoutPacketLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 182400001, count: 5000);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(76, buffer =>
			{
				buffer.WriteQ(1000L);
				buffer.WriteC(1);
			}));

		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_SelectDecomposableDispatchesSelection()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(236, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteD(0);
				buffer.WriteC(1);
			}));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(202, item.ItemId);
				Assert.Equal(3, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 202, expectedCount: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2));
	}

	[Fact]
	public async Task RunAsync_EncryptedSelectDecomposableFrameDispatchesSelection()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(enableCryptKeyBeforeRun: false, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var runTask = Task.Run(() => fixture.Connection.RunAsync());

		await fixture.ReadServerFrameAsync();
		await fixture.WriteClientFrameAsync(
			CreateEncryptedClientFrame(
				CreateClientPayload(236, buffer =>
				{
					buffer.WriteD(5001);
					buffer.WriteD(0);
					buffer.WriteC(1);
				})));

		await WaitUntilAsync(() => player.InventoryItems.Any(item => item.ItemId == 202) && fixture.SentPackets.Count >= 7);
		await fixture.Connection.CloseAsync();
		await AssertCompletesAsync(runTask);
		var sentPackets = fixture.SentPackets.ToArray();

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(202, item.ItemId);
				Assert.Equal(3, item.Count);
			});
		Assert.Collection(
			sentPackets,
			packet => Assert.IsType<SmKey>(packet),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 202, expectedCount: 3),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2));
	}

	[Fact]
	public async Task RunAsync_EncryptedUseItemFrameSchedulesAndCompletesDecompose()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]),
			enableCryptKeyBeforeRun: false);
		var player = CreatePlayer(itemId: 100);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var runTask = Task.Run(() => fixture.Connection.RunAsync());

		await fixture.ReadServerFrameAsync();
		await fixture.WriteClientFrameAsync(
			CreateEncryptedClientFrame(
				CreateClientPayload(37, buffer =>
				{
					buffer.WriteD(5001);
					buffer.WriteC(0);
				})));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 7, TimeSpan.FromSeconds(5));
		await fixture.Connection.CloseAsync();
		await AssertCompletesAsync(runTask);
		var sentPackets = fixture.SentPackets.ToArray();

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(100, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(200, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		Assert.Collection(
			sentPackets,
			packet => Assert.IsType<SmKey>(packet),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 200, expectedCount: 1),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task RunAsync_EncryptedUseItemFrameDeletesLastSourceAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]),
			enableCryptKeyBeforeRun: false);
		var player = CreatePlayer(itemId: 100, count: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var runTask = Task.Run(() => fixture.Connection.RunAsync());

		await fixture.ReadServerFrameAsync();
		await fixture.WriteClientFrameAsync(
			CreateEncryptedClientFrame(
				CreateClientPayload(37, buffer =>
				{
					buffer.WriteD(5001);
					buffer.WriteC(0);
				})));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 8, TimeSpan.FromSeconds(5));
		await fixture.Connection.CloseAsync();
		await AssertCompletesAsync(runTask);
		var sentPackets = fixture.SentPackets.ToArray();

		Assert.Equal(0, player.UsingItemObjectId);
		var reward = Assert.Single(player.InventoryItems);
		Assert.Equal(200, reward.ItemId);
		Assert.Equal(1, reward.Count);
		Assert.Collection(
			sentPackets,
			packet => Assert.IsType<SmKey>(packet),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryAddPayload(Assert.IsType<SmInventoryAddItem>(packet), expectedObjectId: 1, expectedItemId: 200, expectedCount: 1),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CompositeStonesWritesCleanupSealFlagsForRemainingConsumedInputs()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 165010000);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 7001, ItemId = 165010000, Count = 2, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7002, ItemId = 166000080, Count = 2, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7003, ItemId = 166000085, Count = 2, Location = 0, OwnerId = player.ObjectId },
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(208, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteD(7002);
				buffer.WriteD(7003);
			}));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 5, TimeSpan.FromSeconds(6));

		Assert.All(player.InventoryItems, item => Assert.Equal(1, item.Count));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 5000, expectedEnd: 0, expectedItemObjectId: 7001),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 7001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 7002,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 7003,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 0, expectedEnd: 1, expectedItemObjectId: 7001));
	}

	[Fact]
	public async Task ProcessPacketAsync_CompositeStonesSendsConsumedPacketsInJavaOrderForMixedDeletes()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 165010000);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 7001, ItemId = 165010000, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7002, ItemId = 166000080, Count = 2, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7003, ItemId = 166000085, Count = 1, Location = 0, OwnerId = player.ObjectId },
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(208, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteD(7002);
				buffer.WriteD(7003);
			}));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(6));

		var remainingStone = Assert.Single(player.InventoryItems);
		Assert.Equal(7002, remainingStone.ObjectId);
		Assert.Equal(1, remainingStone.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 5000, expectedEnd: 0, expectedItemObjectId: 7001),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 7002,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7003, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 0, expectedEnd: 1, expectedItemObjectId: 7001));
	}

	[Fact]
	public async Task ProcessPacketAsync_CompositeStonesConsumesSameStoneStackTwiceInJavaOrder()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 165010000);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 7001, ItemId = 165010000, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7002, ItemId = 166000080, Count = 2, Location = 0, OwnerId = player.ObjectId },
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(208, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteD(7002);
				buffer.WriteD(7002);
			}));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 7, TimeSpan.FromSeconds(6));

		Assert.Empty(player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 5000, expectedEnd: 0, expectedItemObjectId: 7001),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 7002,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3,
				expectedItemMask: 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7002, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 0, expectedEnd: 1, expectedItemObjectId: 7001));
	}

	[Fact]
	public async Task ProcessPacketAsync_CompositeStonesAddsRewardWithJavaCubeUpdate()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([9001]));
		var player = CreatePlayer(itemId: 165010000);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 7001, ItemId = 165010000, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7002, ItemId = 166000020, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7003, ItemId = 166000030, Count = 1, Location = 0, OwnerId = player.ObjectId },
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(208, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteD(7002);
				buffer.WriteD(7003);
			}));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 9, TimeSpan.FromSeconds(6));

		var reward = Assert.Single(player.InventoryItems);
		var possibleRewardIds = new HashSet<int>(
			Enumerable.Range(166000015, 10).Concat(Enumerable.Range(166000026, 10)));
		Assert.Contains(reward.ItemId, possibleRewardIds);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 5000, expectedEnd: 0, expectedItemObjectId: 7001),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7002, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7003, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryAddPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: reward.ObjectId,
				expectedItemId: reward.ItemId,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.ItemCollect),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 0, expectedEnd: 1, expectedItemObjectId: 7001));
	}

	[Fact]
	public async Task ProcessPacketAsync_CompositeStonesMergesRewardWithoutCubeUpdate()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 165010000);
		var possibleRewardIds = Enumerable.Range(166000015, 10)
			.Concat(Enumerable.Range(166000026, 10))
			.ToArray();
		var seededRewardStacks = possibleRewardIds
			.Select((itemId, index) => new InventoryItem
			{
				ObjectId = 7100 + index,
				ItemId = itemId,
				Count = 1,
				Location = 0,
				OwnerId = player.ObjectId,
			});
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 7001, ItemId = 165010000, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7002, ItemId = 166001020, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7003, ItemId = 166001030, Count = 1, Location = 0, OwnerId = player.ObjectId },
			.. seededRewardStacks,
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(208, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteD(7002);
				buffer.WriteD(7003);
			}));

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 9, TimeSpan.FromSeconds(6));

		Assert.Equal(possibleRewardIds.Length, player.InventoryItems.Count);
		var mergedReward = Assert.Single(
			player.InventoryItems,
			item => possibleRewardIds.Contains(item.ItemId) && item.Count == 2);
		Assert.All(
			player.InventoryItems.Where(item => possibleRewardIds.Contains(item.ItemId) && item.ItemId != mergedReward.ItemId),
			item => Assert.Equal(1, item.Count));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 5000, expectedEnd: 0, expectedItemObjectId: 7001),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 22),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7002, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 21),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7003, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 20),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: mergedReward.ObjectId,
				expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect,
				// Java parity: item_restriction_cleanups.xml does not list the composition
				// enchantment-stone reward ids, so GeneralInfoBlobEntry writes cleanup flag 0.
				expectedCleanupSealFlag: 0,
				expectedItemMask: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 0, expectedEnd: 1, expectedItemObjectId: 7001));
	}

	[Fact]
	public async Task ProcessPacketAsync_CompositeStonesKeepsEarlierConsumesWhenSecondStoneDisappearsBeforeCompletion()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 165010000);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 7001, ItemId = 165010000, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7002, ItemId = 166000080, Count = 1, Location = 0, OwnerId = player.ObjectId },
			new InventoryItem { ObjectId = 7003, ItemId = 166000085, Count = 1, Location = 0, OwnerId = player.ObjectId },
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(208, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteD(7002);
				buffer.WriteD(7003);
			}));
		player.InventoryItems = player.InventoryItems
			.Where(item => item.ObjectId != 7003)
			.ToArray();

		await WaitUntilAsync(() => fixture.SentPackets.Count >= 6, TimeSpan.FromSeconds(6));

		Assert.Empty(player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 5000, expectedEnd: 0, expectedItemObjectId: 7001),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 7002, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 165010000, expectedTime: 0, expectedEnd: 1, expectedItemObjectId: 7001));
	}

	private static Player CreatePlayer(
		int itemId,
		long count = 2,
		string race = "ELYOS",
		string playerClass = "RANGER",
		bool isEquipped = false,
		int location = 0,
		string gender = "MALE",
		int level = 1,
		byte accountMembership = 0,
		int accountId = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			AccountId = accountId,
			Name = "TicketUser",
			AccountMembership = accountMembership,
			Race = race,
			PlayerClass = playerClass,
			Gender = gender,
			Level = level,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = itemId,
					Count = count,
					OwnerId = location == 2 ? accountId : 1001,
					Location = location,
					IsEquipped = isEquipped,
				},
			],
		};
	}

	private static InventoryItem[] CreateStorageFillerItems(int location, int count, int ownerId = 1001, int startObjectId = 6000)
	{
		return Enumerable.Range(0, count)
			.Select(index => new InventoryItem
			{
				ObjectId = startObjectId + index,
				ItemId = 200,
				Count = 1,
				OwnerId = ownerId,
				Location = location,
			})
			.ToArray();
	}

	private static Player CreateApExtractPlayer(long sourceCount = 2)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default(),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = 165005000,
					Count = sourceCount,
					Location = 0,
				},
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 100000363,
					Count = 1,
					Location = 0,
				},
			],
		};
	}

	private static Player CreateAssemblyPlayer(long existingRewardCount = 0)
	{
		var items = new List<InventoryItem>
		{
			new()
			{
				ObjectId = 5001,
				ItemId = 103,
				Count = 1,
				Location = 0,
			},
			new()
			{
				ObjectId = 5100,
				ItemId = 100,
				Count = 2,
				Location = 0,
			},
			new()
			{
				ObjectId = 5101,
				ItemId = 101,
				Count = 2,
				Location = 0,
			},
		};
		if (existingRewardCount > 0)
		{
			items.Add(
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 188053996,
					Count = existingRewardCount,
					Location = 0,
				});
		}

		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems = items.ToArray(),
		};
	}

	private static Player CreateExpExtractPlayer(long existingRewardCount = 0)
	{
		var items = new List<InventoryItem>
		{
			new()
			{
				ObjectId = 5001,
				ItemId = 104,
				Count = 2,
				Location = 0,
			},
		};
		if (existingRewardCount > 0)
		{
			items.Add(
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 188053996,
					Count = existingRewardCount,
					Location = 0,
				});
		}

		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Exp = 150,
			InventoryItems = items.ToArray(),
		};
	}

	private static Player CreateExtractPlayer(long existingRewardCount = 0, long sourceCount = 2)
	{
		var items = new List<InventoryItem>
		{
			new()
			{
				ObjectId = 5001,
				ItemId = 105,
				Count = sourceCount,
				Location = 0,
			},
			new()
			{
				ObjectId = 6200,
				ItemId = 100000500,
				Count = 1,
				Location = 0,
			},
		};
		if (existingRewardCount > 0)
		{
			items.Add(
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 166000195,
					Count = existingRewardCount,
					Location = 0,
				});
		}

		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems = items.ToArray(),
		};
	}

	private static Player CreateChargePaymentPlayer(bool isEquipped = false)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 1000 },
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7001,
					ItemId = 100000400,
					Count = 1,
					Location = 0,
					IsEquipped = isEquipped,
				},
			],
		};
	}

	private static Player CreateChargeAllPaymentPlayer(int charge = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 1000 },
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7001,
					ItemId = 100000400,
					Count = 1,
					Location = 0,
					IsEquipped = true,
					Charge = charge,
				},
			],
		};
	}

	private static Player CreateChargeAllPaymentPlayerWithTwoItems(int firstCharge = 0, int secondCharge = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 1000 },
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7001,
					ItemId = 100000400,
					Count = 1,
					Location = 0,
					IsEquipped = true,
					Charge = firstCharge,
				},
				new InventoryItem
				{
					ObjectId = 7002,
					ItemId = 100000400,
					Count = 1,
					Location = 0,
					IsEquipped = true,
					Charge = secondCharge,
				},
			],
		};
	}

	private static Player CreateKinahChargePaymentPlayer(long kinah)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "KinahUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default(),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7101,
					ItemId = 100000401,
					Count = 1,
					Location = 0,
				},
				new InventoryItem
				{
					ObjectId = 8101,
					ItemId = 182400001,
					Count = kinah,
					Location = 0,
				},
			],
		};
	}

	private static Player CreateKinahChargeAllPaymentPlayer(long kinah)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "KinahUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default(),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7101,
					ItemId = 100000401,
					Count = 1,
					Location = 0,
					IsEquipped = true,
				},
				new InventoryItem
				{
					ObjectId = 8101,
					ItemId = 182400001,
					Count = kinah,
					Location = 0,
				},
			],
		};
	}

	private static Player CreateKinahChargeAllPaymentPlayerWithTwoItems(long kinah, int firstCharge = 0, int secondCharge = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "KinahUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default(),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7101,
					ItemId = 100000401,
					Count = 1,
					Location = 0,
					IsEquipped = true,
					Charge = firstCharge,
				},
				new InventoryItem
				{
					ObjectId = 7102,
					ItemId = 100000401,
					Count = 1,
					Location = 0,
					IsEquipped = true,
					Charge = secondCharge,
				},
				new InventoryItem
				{
					ObjectId = 8101,
					ItemId = 182400001,
					Count = kinah,
					Location = 0,
				},
			],
		};
	}

	private static GameServerOptions CreateApCapOptions()
	{
		return new GameServerOptions
		{
			Custom = new GameServerCustomOptions
			{
				EnableApCap = true,
				ApCapValue = 1_000,
			},
		};
	}

	private static GameServerOptions CreateQuestLimitOptions(int limit, byte disabledMembership = 10)
	{
		return new GameServerOptions
		{
			Custom = new GameServerCustomOptions { BasicQuestSizeLimit = limit },
			Membership = new GameServerMembershipOptions { QuestLimitDisabled = disabledMembership },
		};
	}

	private static CmUseItem CreateUseItem(int sourceItemObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteC(0);
		var packet = new CmUseItem(37, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmUseItem CreateUseItemTarget(int sourceItemObjectId, int targetItemObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteC(2);
		writer.WriteD(targetItemObjectId);
		var packet = new CmUseItem(37, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmChargeItem CreateChargeItem(int itemObjectId, int chargeLevel)
	{
		return CreateChargeItems(chargeLevel, itemObjectId);
	}

	private static CmChargeItem CreateChargeItems(int chargeLevel, params int[] itemObjectIds)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(0);
		writer.WriteC((byte)chargeLevel);
		writer.WriteH(itemObjectIds.Length);
		foreach (var itemObjectId in itemObjectIds)
			writer.WriteD(itemObjectId);
		var packet = new CmChargeItem(78, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmMoveItem CreateMoveItem(int itemObjectId, byte source, byte destination, short slot)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(itemObjectId);
		writer.WriteC(source);
		writer.WriteC(destination);
		writer.WriteH(slot);
		var packet = new CmMoveItem(30, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmReplaceItem CreateReplaceItem(byte sourceStorageType, int sourceItemObjectId, byte replaceStorageType, int replaceItemObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteC(sourceStorageType);
		writer.WriteD(sourceItemObjectId);
		writer.WriteC(replaceStorageType);
		writer.WriteD(replaceItemObjectId);
		var packet = new CmReplaceItem(178, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmSplitItem CreateSplitItem(
		int sourceItemObjectId,
		long itemAmount,
		byte sourceStorageType,
		int destinationItemObjectId,
		byte destinationStorageType,
		short slotNumber)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteQ(itemAmount);
		writer.WriteC(sourceStorageType);
		writer.WriteD(destinationItemObjectId);
		writer.WriteC(destinationStorageType);
		writer.WriteH(slotNumber);
		var packet = new CmSplitItem(157, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmQuestionResponse CreateQuestionResponse(int questionId, byte response)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(questionId);
		writer.WriteC(response);
		writer.WriteC(0);
		writer.WriteH(0);
		writer.WriteD(1001);
		writer.WriteD(0);
		writer.WriteH(0);
		var packet = new CmQuestionResponse(104, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmEmotion CreateEmotion(EmotionType emotionType)
	{
		using var writer = new PacketBuffer();
		writer.WriteC((byte)emotionType);
		var packet = new CmEmotion(43, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmSelectDecomposable CreateSelectDecomposable(int sourceItemObjectId, int index)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteD(0);
		writer.WriteC(index);
		var packet = new CmSelectDecomposable(236, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] CreateEncryptedClientFrame(byte[] payload)
	{
		var encryptedPayload = payload.ToArray();
		EncryptClientPayload(encryptedPayload, 0x01020304);
		return GamePacketFrameCodec.CreateFrame(encryptedPayload);
	}

	private static void EncryptClientPayload(Span<byte> data, int baseKey)
	{
		var staticKey = Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");
		Span<byte> clientKey =
		[
			(byte)(baseKey & 0xff),
			(byte)((baseKey >> 8) & 0xff),
			(byte)((baseKey >> 16) & 0xff),
			(byte)((baseKey >> 24) & 0xff),
			0xa1,
			0x6c,
			0x54,
			0x87,
		];

		if (data.Length == 0)
			return;

		data[0] ^= clientKey[0];
		var previous = data[0];

		for (var i = 1; i < data.Length; i++)
		{
			data[i] ^= (byte)(staticKey[i & 63] ^ clientKey[i & 7] ^ previous);
			previous = data[i];
		}

		UpdateClientKey(clientKey, data.Length);
	}

	private static void UpdateClientKey(Span<byte> key, int packetSize)
	{
		var oldKey = BinaryPrimitives.ReadUInt64LittleEndian(key);
		oldKey += (uint)packetSize;
		BinaryPrimitives.WriteUInt64LittleEndian(key, oldKey);
	}

	private static async Task InvokeHandleEmotionAsync(GameServerConnection connection, Player player, CmEmotion packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleEmotionAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeHandleSelectDecomposableAsync(GameServerConnection connection, Player player, CmSelectDecomposable packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleSelectDecomposableAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeHandleChargeItemAsync(GameServerConnection connection, Player player, CmChargeItem packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleChargeItemAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeHandleMoveItemAsync(GameServerConnection connection, Player player, CmMoveItem packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleMoveItemAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeHandleReplaceItemAsync(GameServerConnection connection, Player player, CmReplaceItem packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleReplaceItemAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeHandleSplitItemAsync(GameServerConnection connection, Player player, CmSplitItem packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleSplitItemAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"ProcessPacketAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	private static void SetActivePlayerForPacketDispatch(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		Assert.NotNull(stateField);
		activePlayerField.SetValue(connection, player);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private static void AssertItemUsagePayload(
		SmItemUsageAnimation packet,
		int expectedItemId = 188500000,
		int expectedTime = 0,
		int expectedEnd = 0,
		int expectedUnknown3 = 0,
		int expectedItemObjectId = 5001)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(expectedItemObjectId, reader.ReadD());
		Assert.Equal(expectedItemId, reader.ReadD());
		Assert.Equal(expectedTime, reader.ReadD());
		Assert.Equal(expectedEnd, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(expectedUnknown3, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertQuestActionPacket(
		SmQuestAction packet,
		int expectedActionId,
		int expectedQuestId,
		int expectedClientQuestVars = 0)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedActionId, (int)reader.ReadC());
		Assert.Equal(expectedQuestId, reader.ReadD());
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(expectedClientQuestVars, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertInventoryUpdatePayload(SmInventoryUpdateItem packet, int expectedObjectId, int expectedUpdateType)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(expectedObjectId, reader.ReadD());
		var actualUpdateType = payload[^2] | (payload[^1] << 8);
		Assert.Equal(expectedUpdateType, actualUpdateType);
	}

	private static void AssertWarehouseUpdatePayload(
		SmWarehouseUpdateItem packet,
		int expectedObjectId,
		int expectedWarehouseType,
		int expectedUpdateType)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedWarehouseType, (int)reader.ReadC());
		reader.ReadS();
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		reader.ReadB(blobSize);
		Assert.Equal(expectedUpdateType, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertWarehouseAddPayload(
		SmWarehouseAddItem packet,
		int expectedObjectId,
		int expectedWarehouseType,
		int expectedAddType,
		int? expectedItemId = null,
		long? expectedCount = null,
		int? expectedSlot = null)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedWarehouseType, (int)reader.ReadC());
		Assert.Equal(expectedAddType, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedObjectId, reader.ReadD());
		var actualItemId = reader.ReadD();
		Assert.Equal(expectedItemId ?? actualItemId, actualItemId);
		Assert.Equal(0, (int)reader.ReadC());
		reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		var actualSlot = reader.ReadH();
		Assert.Equal(expectedSlot ?? actualSlot, actualSlot);
		Assert.Equal(0, reader.Remaining);
		if (expectedCount.HasValue)
		{
			using var blobReader = new PacketBuffer(blob);
			Assert.Equal(0, (int)blobReader.ReadC());
			blobReader.ReadH();
			Assert.Equal(expectedCount.Value, blobReader.ReadQ());
		}
	}

	private static void AssertInventoryUpdatePayloadWithCleanupSealFlag(
		SmInventoryUpdateItem packet,
		int expectedObjectId,
		int expectedUpdateType,
		int expectedCleanupSealFlag,
		int expectedItemMask = 123)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		Assert.Equal(expectedUpdateType, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask, expectedCleanupSealFlag);
	}

	private static void AssertChargeInventoryUpdatePayload(SmInventoryUpdateItem packet, int expectedObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		reader.ReadS();
		var blobSize = reader.ReadH();
		reader.ReadB(blobSize);
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertInventoryAddPayload(
		SmInventoryAddItem packet,
		int expectedObjectId,
		int expectedItemId,
		long expectedCount,
		int expectedAddType = SmInventoryAddItem.Decomposable,
		int expectedSlot = 65535)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedAddType, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedItemId, reader.ReadD());
		reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		Assert.Equal(expectedSlot, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);

		using var blobReader = new PacketBuffer(blob);
		Assert.Equal(0, (int)blobReader.ReadC());
		blobReader.ReadH();
		Assert.Equal(expectedCount, blobReader.ReadQ());
	}

	private static void AssertDecomposableAddPayloadWithCleanupSealFlag(
		SmInventoryAddItem packet,
		int expectedObjectId,
		int expectedItemId,
		long expectedCount,
		int expectedCleanupSealFlag)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(SmInventoryAddItem.Decomposable, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedItemId, reader.ReadD());
		reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		Assert.Equal(65535, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);

		using var blobReader = new PacketBuffer(blob);
		Assert.Equal(0, (int)blobReader.ReadC());
		blobReader.ReadH();
		Assert.Equal(expectedCount, blobReader.ReadQ());
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask: 123, expectedFlag: expectedCleanupSealFlag);
	}

	private static void AssertInventoryItemCollectAddPayload(
		SmInventoryAddItem packet,
		int expectedObjectId,
		int expectedItemId,
		long expectedCount,
		int expectedCleanupSealFlag)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(SmInventoryAddItem.ItemCollect, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedItemId, reader.ReadD());
		reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		Assert.Equal(65535, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);

		using var blobReader = new PacketBuffer(blob);
		Assert.Equal(0, (int)blobReader.ReadC());
		blobReader.ReadH();
		Assert.Equal(expectedCount, blobReader.ReadQ());
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask: 123, expectedFlag: expectedCleanupSealFlag);
	}

	private static void AssertGeneralInfoCleanupSealFlag(byte[] blob, int expectedItemMask, int expectedFlag)
	{
		using var blobReader = new PacketBuffer(blob);
		Assert.Equal(0x00, (int)blobReader.ReadC());
		Assert.Equal(expectedItemMask, blobReader.ReadH());
		blobReader.ReadQ();
		Assert.Equal(string.Empty, blobReader.ReadS());
		Assert.Equal(0, (int)blobReader.ReadC());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(expectedFlag, blobReader.ReadH());
	}

	private static void AssertDeleteItemPayload(SmDeleteItem packet, int expectedObjectId, int expectedDeleteType)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedDeleteType, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertDeleteWarehouseItemPayload(
		SmDeleteWarehouseItem packet,
		int expectedWarehouseType,
		int expectedObjectId,
		int expectedDeleteType)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedWarehouseType, (int)reader.ReadC());
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedDeleteType, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertCubeUpdatePayload(
		SmCubeUpdate packet,
		int expectedItemsCount,
		int expectedNpcExpands = 0,
		int expectedQuestExpands = 0,
		int expectedItemExpands = 0,
		int expectedStorage = 0)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(expectedStorage, (int)reader.ReadC());
		Assert.Equal(expectedItemsCount, reader.ReadD());
		Assert.Equal(expectedNpcExpands, (int)reader.ReadC());
		Assert.Equal(expectedQuestExpands, (int)reader.ReadC());
		Assert.Equal(expectedItemExpands, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSystemMessagePayload(SmSystemMessage packet, int expectedMessageId, params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSkillCancelPayload(SmSkillCancel packet, int expectedCreatureObjectId, int expectedSkillId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedCreatureObjectId, reader.ReadD());
		Assert.Equal(expectedSkillId, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertFirstShowDecomposablePayload(SmFirstShowDecomposable packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(201, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(202, reader.ReadD());
		Assert.Equal(3, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSecondaryShowDecomposablePayload(SmSecondaryShowDecomposable packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan? timeout = null)
	{
		var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(3));
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (predicate())
				return;

			await Task.Delay(25);
		}

		Assert.True(predicate());
	}

	private static async Task AssertCompletesAsync(Task task)
	{
		var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(task, completed);
		await task;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static async Task<string> CaptureSelectableDecomposeObservationJsonAsync(
		string scenarioId,
		int sourceCount,
		int selectIndex,
		SelectableDecomposeTestData? selectableData = null)
	{
		var selectableFixture = selectableData ?? SelectableDecomposeTestData.Default;
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]), selectableData: selectableFixture);
		var player = CreatePlayer(itemId: selectableFixture.SourceItemId, count: sourceCount);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, selectIndex));

		var observation = new
		{
			schema_version = 1,
			scenario_id = scenarioId,
			capture_method = "csharp-test-observer",
			capture_levels = new[] { 1, 2 },
			fixture = new
			{
				player = new
				{
					object_id = 1001,
					name = "Player",
					level = 10,
					race = "ELYOS",
				},
				known_list = new
				{
					visible_players = Array.Empty<int>(),
				},
				initial_inventory = new[]
				{
					new
					{
						object_id = 5001,
						item_id = selectableFixture.SourceItemId,
						count = sourceCount,
						location = "CUBE",
						equipped = false,
					},
				},
			},
			client_packet = new
			{
				@class = "CM_SELECT_DECOMPOSABLE",
				opcode = 236,
				payload_fields = new
				{
					object_id = 5001,
					unknown_dword = 0,
					index = selectIndex,
				},
			},
			packets = fixture.SentPackets
				.Select((packet, index) => BuildPacketObservation(index + 1, packet))
				.ToArray(),
			final_inventory = player.InventoryItems
				.OrderBy(item => item.ObjectId)
				.Select(item => new
				{
					object_id = item.ObjectId,
					item_id = item.ItemId,
					count = item.Count,
					location = item.Location == 0 ? "CUBE" : item.Location.ToString(),
				})
				.ToArray(),
			unsupported = new[]
			{
				"Java runtime artifact not captured in this C# projection",
				"unencrypted body bytes omitted from JSON projection",
				"encrypted frame bytes omitted from JSON projection",
			},
			risks = new[]
			{
				"C# projection is comparison-readiness evidence only and cannot verify Java parity by itself",
			},
		};

		return JsonSerializer.Serialize(observation, new JsonSerializerOptions { WriteIndented = true });
	}

	private static Dictionary<string, object?> BuildPacketObservation(int sequence, GameServerPacket packet)
	{
		return new Dictionary<string, object?>
		{
			["sequence"] = sequence,
			["recipient_object_id"] = 1001,
			["java_class"] = ToJavaPacketClass(packet),
			["decoded_fields"] = DecodePacketFields(packet),
			["unencrypted_body_hex"] = null,
			["encrypted_frame_hex"] = null,
			["notes"] = Array.Empty<string>(),
		};
	}

	private static string ToJavaPacketClass(GameServerPacket packet)
	{
		return packet switch
		{
			SmItemUsageAnimation => "SM_ITEM_USAGE_ANIMATION",
			SmSystemMessage => "SM_SYSTEM_MESSAGE",
			SmInventoryUpdateItem => "SM_INVENTORY_UPDATE_ITEM",
			SmDeleteItem => "SM_DELETE_ITEM",
			SmCubeUpdate => "SM_CUBE_UPDATE",
			SmSecondaryShowDecomposable => "SM_SECONDARY_SHOW_DECOMPOSABLE",
			SmInventoryAddItem => "SM_INVENTORY_ADD_ITEM",
			_ => packet.GetType().Name,
		};
	}

	private static Dictionary<string, object?> DecodePacketFields(GameServerPacket packet)
	{
		return packet switch
		{
			SmItemUsageAnimation itemUsage => DecodeItemUsageFields(itemUsage),
			SmSystemMessage systemMessage => DecodeSystemMessageFields(systemMessage),
			SmInventoryUpdateItem inventoryUpdate => DecodeInventoryUpdateFields(inventoryUpdate),
			SmDeleteItem deleteItem => DecodeDeleteItemFields(deleteItem),
			SmCubeUpdate cubeUpdate => DecodeCubeUpdateFields(cubeUpdate),
			SmSecondaryShowDecomposable secondaryShow => DecodeSecondaryShowFields(secondaryShow),
			SmInventoryAddItem addItem => DecodeInventoryAddFields(addItem),
			_ => new Dictionary<string, object?>(),
		};
	}

	private static Dictionary<string, object?> DecodeItemUsageFields(SmItemUsageAnimation packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		return new Dictionary<string, object?>
		{
			["player_object_id"] = reader.ReadD(),
			["target_object_id"] = reader.ReadD(),
			["item_object_id"] = reader.ReadD(),
			["item_id"] = reader.ReadD(),
			["time"] = reader.ReadD(),
			["end"] = (int)reader.ReadC(),
			["unknown"] = (int)reader.ReadC(),
			["unknown1"] = (int)reader.ReadC(),
			["unknown2"] = (int)reader.ReadC(),
			["unknown3"] = reader.ReadD(),
		};
	}

	private static Dictionary<string, object?> DecodeSystemMessageFields(SmSystemMessage packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		var chatType = (int)reader.ReadC();
		var messageKind = (int)reader.ReadC();
		var senderObjectId = reader.ReadD();
		var messageId = reader.ReadD();
		var fields = new Dictionary<string, object?>
		{
			["chat_type"] = chatType,
			["message_kind"] = messageKind,
			["sender_object_id"] = senderObjectId,
			["message_id"] = messageId,
			["factory_name"] = GetSystemMessageFactoryName(messageId),
		};
		var parameterCount = (int)reader.ReadC();
		var parameters = new string[parameterCount];
		for (var index = 0; index < parameterCount; index++)
			parameters[index] = reader.ReadS();
		fields["parameters"] = parameters;
		fields["trailing_flag"] = (int)reader.ReadC();
		return fields;
	}

	private static string? GetSystemMessageFactoryName(int messageId)
	{
		return messageId switch
		{
			// Java parity: SM_SYSTEM_MESSAGE.STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED(String) -> 1400452.
			1400452 => "STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED",
			// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_INVENTORY_IS_FULL() -> 1300447.
			1300447 => "STR_DECOMPOSE_ITEM_INVENTORY_IS_FULL",
			_ => null,
		};
	}

	private static Dictionary<string, object?> DecodeInventoryUpdateFields(SmInventoryUpdateItem packet)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		var objectId = reader.ReadD();
		var itemName = reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		var updateType = reader.ReadH();
		var (_, count) = DecodeGeneralInfoBlobCount(blob);
		return new Dictionary<string, object?>
		{
			["object_id"] = objectId,
			["item_name"] = itemName,
			["count"] = count,
			["update_type_mask"] = updateType,
			["update_type_name"] = updateType == SmInventoryUpdateItem.DecreaseItemUse ? "DEC_ITEM_USE" : updateType.ToString(),
		};
	}

	private static Dictionary<string, object?> DecodeDeleteItemFields(SmDeleteItem packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		return new Dictionary<string, object?>
		{
			["object_id"] = reader.ReadD(),
			["delete_type"] = (int)reader.ReadC(),
			["delete_type_name"] = "USE",
		};
	}

	private static Dictionary<string, object?> DecodeCubeUpdateFields(SmCubeUpdate packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		return new Dictionary<string, object?>
		{
			["action"] = (int)reader.ReadC(),
			["storage"] = (int)reader.ReadC(),
			["items_count"] = reader.ReadD(),
			["npc_expands"] = (int)reader.ReadC(),
			["quest_expands"] = (int)reader.ReadC(),
			["item_expands"] = (int)reader.ReadC(),
		};
	}

	private static Dictionary<string, object?> DecodeSecondaryShowFields(SmSecondaryShowDecomposable packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		return new Dictionary<string, object?>
		{
			["source_object_id"] = reader.ReadD(),
			["unknown_dword"] = reader.ReadD(),
			["reward_count"] = (int)reader.ReadC(),
		};
	}

	private static Dictionary<string, object?> DecodeInventoryAddFields(SmInventoryAddItem packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		var addType = reader.ReadH();
		var itemCount = reader.ReadH();
		var objectId = reader.ReadD();
		var itemId = reader.ReadD();
		var itemName = reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		var slot = reader.ReadH();
		var clothFlag = (int)reader.ReadC();
		var (_, count) = DecodeGeneralInfoBlobCount(blob);
		return new Dictionary<string, object?>
		{
			["add_type_mask"] = addType,
			["add_type_name"] = "DECOMPOSABLE",
			["packet_item_count"] = itemCount,
			["object_id"] = objectId,
			["item_id"] = itemId,
			["item_name"] = itemName,
			["count"] = count,
			["slot"] = slot,
			["cloth_flag"] = clothFlag,
		};
	}

	private static (int Mask, long Count) DecodeGeneralInfoBlobCount(byte[] blob)
	{
		using var reader = new PacketBuffer(blob);
		Assert.Equal(0, (int)reader.ReadC());
		var mask = reader.ReadH();
		var count = reader.ReadQ();
		return (mask, count);
	}

	private static string[] ReadPacketClasses(JsonElement observation)
	{
		return observation
			.GetProperty("packets")
			.EnumerateArray()
			.Select(packet => packet.GetProperty("java_class").GetString()!)
			.ToArray();
	}

	private static JsonElement GetPacket(JsonElement observation, int sequence)
	{
		return observation
			.GetProperty("packets")
			.EnumerateArray()
			.Single(packet => packet.GetProperty("sequence").GetInt32() == sequence);
	}

	private static string CreateMappedSelectableDecomposeJavaArtifactJson(
		string csharpJson,
		int sourceItemId,
		int rewardItemId,
		int? sourceObjectId = null,
		int? rewardObjectId = null,
		bool includeRewardAddTrailingCubeUpdate = false)
	{
		var artifact = JsonNode.Parse(csharpJson)!.AsObject();
		artifact["capture_method"] = "live-java-server";
		var fixture = artifact["fixture"]!.AsObject();
		var idMapping = new JsonObject
		{
			["logical_source_item_id"] = 101,
			["java_source_item_id"] = sourceItemId,
			["logical_reward_index_1"] = 202,
			["java_reward_index_1"] = rewardItemId,
		};
		fixture["id_mapping"] = idMapping;

		SetPacketField(artifact, sequence: 1, "item_id", sourceItemId);
		SetPacketField(artifact, sequence: 5, "item_id", rewardItemId);
		if (sourceObjectId.HasValue)
		{
			idMapping["logical_source_object_id"] = 5001;
			idMapping["java_source_object_id"] = sourceObjectId.Value;
			SetClientPacketPayloadField(artifact, "object_id", sourceObjectId.Value);
			SetPacketField(artifact, sequence: 1, "item_object_id", sourceObjectId.Value);
			SetPacketField(artifact, sequence: 3, "object_id", sourceObjectId.Value);
			SetPacketField(artifact, sequence: 4, "source_object_id", sourceObjectId.Value);
		}
		if (rewardObjectId.HasValue)
		{
			idMapping["logical_reward_index_1_object_id"] = 1;
			idMapping["java_reward_index_1_object_id"] = rewardObjectId.Value;
			SetPacketField(artifact, sequence: 5, "object_id", rewardObjectId.Value);
		}
		if (includeRewardAddTrailingCubeUpdate)
			AppendRewardAddTrailingCubeUpdate(artifact);
		return artifact.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
	}

	private static void SetClientPacketPayloadField(JsonObject observation, string fieldName, int value)
	{
		observation["client_packet"]!.AsObject()["payload_fields"]!.AsObject()[fieldName] = value;
	}

	private static void AppendRewardAddTrailingCubeUpdate(JsonObject observation)
	{
		var packets = observation["packets"]!.AsArray();
		var nextSequence = packets.Count + 1;
		packets.Add(new JsonObject
		{
			["sequence"] = nextSequence,
			["recipient_object_id"] = 1001,
			["java_class"] = "SM_CUBE_UPDATE",
			["decoded_fields"] = new JsonObject
			{
				["action"] = 0,
				["storage"] = 0,
				["items_count"] = 2,
				["npc_expands"] = 0,
				["quest_expands"] = 0,
				["item_expands"] = 0,
			},
			["unencrypted_body_hex"] = null,
			["encrypted_frame_hex"] = null,
			["notes"] = new JsonArray("Synthetic Java artifact shape for ItemPacketService.sendStorageUpdatePacket reward-add trailing cube update."),
		});
	}

	private static void SetPacketField(JsonObject observation, int sequence, string fieldName, int value)
	{
		var packets = observation["packets"]!.AsArray();
		var packet = packets
			.Select(node => node!.AsObject())
			.Single(node => node["sequence"]!.GetValue<int>() == sequence);
		packet["decoded_fields"]!.AsObject()[fieldName] = value;
	}

	private static void AssertSelectableDecomposeObservationMatchesJavaArtifact(JsonElement javaObservation, JsonElement csharpObservation)
	{
		var idMapping = ReadIdMapping(javaObservation);
		Assert.Equal(csharpObservation.GetProperty("scenario_id").GetString(), javaObservation.GetProperty("scenario_id").GetString());
		Assert.Equal("live-java-server", javaObservation.GetProperty("capture_method").GetString());
		var csharpPacketClasses = ReadPacketClasses(csharpObservation);
		var javaPacketClasses = ReadPacketClasses(javaObservation);
		ThrowIfJavaObservedRewardAddTrailingCubeUpdate(csharpPacketClasses, javaPacketClasses);
		Assert.Equal(csharpPacketClasses, javaPacketClasses);
		Assert.Equal(
			csharpObservation.GetProperty("client_packet").GetProperty("opcode").GetInt32(),
			javaObservation.GetProperty("client_packet").GetProperty("opcode").GetInt32());
		Assert.Equal(
			csharpObservation.GetProperty("client_packet").GetProperty("payload_fields").GetProperty("index").GetInt32(),
			javaObservation.GetProperty("client_packet").GetProperty("payload_fields").GetProperty("index").GetInt32());
		Assert.Equal(
			csharpObservation.GetProperty("client_packet").GetProperty("payload_fields").GetProperty("unknown_dword").GetInt32(),
			javaObservation.GetProperty("client_packet").GetProperty("payload_fields").GetProperty("unknown_dword").GetInt32());
		AssertMappedJsonValue(
			csharpObservation.GetProperty("client_packet").GetProperty("payload_fields").GetProperty("object_id"),
			javaObservation.GetProperty("client_packet").GetProperty("payload_fields").GetProperty("object_id"),
			idMapping,
			"client packet object_id");

		var scenarioId = csharpObservation.GetProperty("scenario_id").GetString();
		switch (scenarioId)
		{
			case "JD-SEL-DEC-001":
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 1, ["item_object_id", "item_id", "time", "end", "unknown3"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 2, ["message_id", "factory_name"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 3, ["object_id", "count", "update_type_mask", "update_type_name"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 4, ["source_object_id", "reward_count"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 5, ["add_type_mask", "add_type_name", "packet_item_count", "object_id", "item_id", "count", "slot", "cloth_flag"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 6, ["action", "storage", "items_count", "npc_expands", "quest_expands", "item_expands"]);
				break;
			case "JD-SEL-DEL-001":
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 1, ["item_object_id", "item_id", "time", "end", "unknown3"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 2, ["message_id", "factory_name"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 3, ["object_id", "delete_type", "delete_type_name"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 4, ["action", "storage", "items_count", "npc_expands", "quest_expands", "item_expands"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 5, ["source_object_id", "reward_count"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 6, ["add_type_mask", "add_type_name", "packet_item_count", "object_id", "item_id", "count", "slot", "cloth_flag"]);
				AssertPacketFields(javaObservation, csharpObservation, idMapping, sequence: 7, ["action", "storage", "items_count", "npc_expands", "quest_expands", "item_expands"]);
				break;
			default:
				throw new InvalidOperationException("Unsupported selectable-decompose scenario: " + scenarioId);
		}
	}

	private static void ThrowIfJavaObservedRewardAddTrailingCubeUpdate(IReadOnlyList<string> csharpPacketClasses, IReadOnlyList<string> javaPacketClasses)
	{
		if (csharpPacketClasses.Count == 0
			|| javaPacketClasses.Count != csharpPacketClasses.Count + 1
			|| csharpPacketClasses[^1] != "SM_INVENTORY_ADD_ITEM"
			|| javaPacketClasses[^1] != "SM_CUBE_UPDATE")
			return;

		for (var index = 0; index < csharpPacketClasses.Count; index++)
		{
			if (javaPacketClasses[index] != csharpPacketClasses[index])
				return;
		}

		throw new InvalidOperationException(
			"Java artifact includes reward-add trailing SM_CUBE_UPDATE after SM_INVENTORY_ADD_ITEM. "
			+ "Java source ItemPacketService.sendStorageUpdatePacket sends SM_INVENTORY_ADD_ITEM and then SM_CUBE_UPDATE for cube storage; "
			+ "treat this as a C# parity gap to implement or explicitly document, not as an optional packet to ignore.");
	}

	private static Dictionary<int, int> ReadIdMapping(JsonElement javaObservation)
	{
		if (!javaObservation.TryGetProperty("fixture", out var fixture)
			|| !fixture.TryGetProperty("id_mapping", out var idMapping)
			|| idMapping.ValueKind != JsonValueKind.Object)
			return [];

		var mappings = new Dictionary<int, int>();
		foreach (var property in idMapping.EnumerateObject())
		{
			if (!property.Name.StartsWith("java_", StringComparison.Ordinal) || property.Value.ValueKind != JsonValueKind.Number)
				continue;

			var logicalName = "logical_" + property.Name["java_".Length..];
			if (idMapping.TryGetProperty(logicalName, out var logicalValue)
				&& logicalValue.ValueKind == JsonValueKind.Number)
			{
				mappings[property.Value.GetInt32()] = logicalValue.GetInt32();
			}
		}

		return mappings;
	}

	private static void AssertPacketFields(
		JsonElement javaObservation,
		JsonElement csharpObservation,
		IReadOnlyDictionary<int, int> idMapping,
		int sequence,
		IReadOnlyList<string> fieldNames)
	{
		var javaFields = GetPacket(javaObservation, sequence).GetProperty("decoded_fields");
		var csharpFields = GetPacket(csharpObservation, sequence).GetProperty("decoded_fields");
		foreach (var fieldName in fieldNames)
		{
			Assert.True(javaFields.TryGetProperty(fieldName, out var javaValue), $"Java artifact packet {sequence} is missing decoded field '{fieldName}'.");
			Assert.True(csharpFields.TryGetProperty(fieldName, out var csharpValue), $"C# observation packet {sequence} is missing decoded field '{fieldName}'.");
			AssertMappedJsonValue(csharpValue, javaValue, idMapping, $"packet {sequence} decoded field '{fieldName}'");
		}
	}

	private static void AssertMappedJsonValue(JsonElement csharpValue, JsonElement javaValue, IReadOnlyDictionary<int, int> idMapping, string fieldDescription)
	{
		Assert.Equal(csharpValue.ToString(), NormalizeJavaFieldValue(javaValue, idMapping));
	}

	private static string NormalizeJavaFieldValue(JsonElement javaValue, IReadOnlyDictionary<int, int> idMapping)
	{
		if (javaValue.ValueKind == JsonValueKind.Number
			&& javaValue.TryGetInt32(out var numericValue)
			&& idMapping.TryGetValue(numericValue, out var logicalValue))
			return logicalValue.ToString();

		return javaValue.ToString();
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, ".git")) && Directory.Exists(Path.Combine(directory.FullName, "docs")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
	}

	private readonly record struct SelectableDecomposeArtifactScenario(string ScenarioId, int SourceCount, int SelectIndex);

	private sealed record SelectableDecomposeTestData(
		int SourceItemId,
		int RewardIndex0ItemId,
		int RewardIndex0Count,
		int RewardIndex1ItemId,
		int RewardIndex1Count)
	{
		public static SelectableDecomposeTestData Default { get; } = new(101, 201, 2, 202, 3);

		// Java source-of-truth breadcrumb: data/static_data/decomposable_items/decomposable_items.xml
		// Smart Greater Scroll Bundle -> Greater Running Scroll x100 / Greater Courage Scroll x100.
		public static SelectableDecomposeTestData RealJavaXmlCandidate { get; } = new(188051516, 164000076, 100, 164000073, 100);
	}

	private sealed class InventoryExpansionUseItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly ThreadPoolManager? _threadPoolManager;
		private readonly string _tempRoot;

		private InventoryExpansionUseItemFixture(
			TcpClient client,
			GameServerConnection connection,
			ThreadPoolManager? threadPoolManager,
			List<GameServerPacket> sentPackets,
			StaticData staticData,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			_threadPoolManager = threadPoolManager;
			SentPackets = sentPackets;
			StaticData = staticData;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public List<GameServerPacket> SentPackets { get; }

		public StaticData StaticData { get; }

		public static async Task<InventoryExpansionUseItemFixture> CreateAsync(
			EmptyPlayerEnterWorldRepository? repository = null,
			bool includeThreadPoolManager = false,
			IDFactory? idFactory = null,
			bool enableCryptKeyBeforeRun = true,
			SelectableDecomposeTestData? selectableData = null,
			GameServerOptions? options = null,
			int extractionRewardMaxStackCount = 100,
			bool includeExtractionRewardTemplate = true,
			Func<bool>? isShuttingDownSoon = null)
		{
			options ??= new GameServerOptions();
			var selectableFixture = selectableData ?? SelectableDecomposeTestData.Default;
			var extractionRewardTemplateXml = includeExtractionRewardTemplate
				? $"""<item_template id="166000195" name="Restricted Extraction Reward" level="1" mask="123" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="{extractionRewardMaxStackCount}"/>"""
				: string.Empty;
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-inventory-expansion-use-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				$"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<player_experience_table>
						<exp>0</exp>
						<exp>100</exp>
					</player_experience_table>
					<item_templates>
						<item_template id="152200001" name="Test Craft Recipe" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<craftlearn recipeid="155000001"/>
							</actions>
						</item_template>
						<item_template id="169500001" name="Test Skill Book" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<skilllearn skillid="1" level="1" class="RANGER"/>
							</actions>
						</item_template>
						<item_template id="169700001" name="Test Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1114"/>
							</actions>
						</item_template>
						<item_template id="169700002" name="Test Repeat Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1115"/>
							</actions>
						</item_template>
						<item_template id="169700003" name="Test Race Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1116"/>
							</actions>
						</item_template>
						<item_template id="169700004" name="Test Min Level Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1117"/>
							</actions>
						</item_template>
						<item_template id="169700005" name="Test Max Level Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1118"/>
							</actions>
						</item_template>
						<item_template id="169700006" name="Test Class Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1119"/>
							</actions>
						</item_template>
						<item_template id="169700007" name="Test Gender Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1120"/>
							</actions>
						</item_template>
						<item_template id="169700008" name="Test No Count Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1122"/>
							</actions>
						</item_template>
						<item_template id="169700009" name="Test Inventory Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1123"/>
							</actions>
						</item_template>
						<item_template id="169700010" name="Test Combine Skill Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1124"/>
							</actions>
						</item_template>
						<item_template id="169700011" name="Test Rank Quest Starter" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<queststart questid="1125"/>
							</actions>
						</item_template>
						<item_template id="182215001" name="Required Quest Token" desc="910001" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10"/>
						<item_template id="169600001" name="Test Emotion Card" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<learnemotion emotionid="64"/>
							</actions>
						</item_template>
						<item_template id="169945000" name="Test Title Card" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<titleadd titleid="269"/>
							</actions>
						</item_template>
						<item_template id="169630000" name="[Expand Card] Expand Cube Ticket (lvl 1)" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<expandinventory level="1" storage="CUBE" />
							</actions>
						</item_template>
						<item_template id="169640000" name="[Expand Card] Expand Warehouse Ticket (lvl 1)" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<expandinventory level="1" storage="WAREHOUSE" />
							</actions>
						</item_template>
						<item_template id="188500000" name="[Motion Card] Test Motion" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<animation idle="1" run="2" jump="3" rest="4" minutes="60" />
							</actions>
						</item_template>
						<item_template id="165005000" name="Test AP Extraction Tool" level="40" item_group="NONE" item_type="NORMAL" quality="RARE" race="PC_ALL" max_stack_count="10">
							<actions>
								<apextract target="WEAPON" rate="0.2" />
							</actions>
						</item_template>
						<item_template id="165010000" name="Test Composition Tool" level="1" item_group="COMBINATION" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<composition/>
							</actions>
						</item_template>
						<item_template id="166000020" name="Test Enchantment Stone 20" level="20" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000030" name="Test Enchantment Stone 30" level="30" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166001020" name="Test Input-Only Enchantment Stone 20" level="20" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166001030" name="Test Input-Only Enchantment Stone 30" level="30" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000015" name="Test Enchantment Stone 15" level="15" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000016" name="Test Enchantment Stone 16" level="16" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000017" name="Test Enchantment Stone 17" level="17" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000018" name="Test Enchantment Stone 18" level="18" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000019" name="Test Enchantment Stone 19" level="19" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000021" name="Test Enchantment Stone 21" level="21" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000022" name="Test Enchantment Stone 22" level="22" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000023" name="Test Enchantment Stone 23" level="23" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000024" name="Test Enchantment Stone 24" level="24" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000026" name="Test Enchantment Stone 26" level="26" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000027" name="Test Enchantment Stone 27" level="27" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000028" name="Test Enchantment Stone 28" level="28" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000029" name="Test Enchantment Stone 29" level="29" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000031" name="Test Enchantment Stone 31" level="31" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000032" name="Test Enchantment Stone 32" level="32" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000033" name="Test Enchantment Stone 33" level="33" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000034" name="Test Enchantment Stone 34" level="34" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000035" name="Test Enchantment Stone 35" level="35" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000080" name="Test Enchantment Stone 80" level="80" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="166000085" name="Test Enchantment Stone 85" level="85" item_group="ENCHANTMENT" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="100000363" name="Test Abyss Sword" level="30" mask="65536" item_group="SWORD" item_type="ABYSS" quality="RARE" race="PC_ALL" max_stack_count="1">
							<acquisition ap="4900" />
						</item_template>
						<item_template id="100000400" name="Test Conditioning Sword" level="30" item_group="SWORD" item_type="ABYSS" quality="RARE" race="PC_ALL" max_stack_count="1">
							<improve way="2" level="2" burn_attack="0" burn_defend="0" price1="1000" price2="2000" />
						</item_template>
						<item_template id="100000401" name="Test Kinah Conditioning Sword" level="30" item_group="SWORD" item_type="NORMAL" quality="RARE" race="PC_ALL" max_stack_count="1">
							<improve way="1" level="2" burn_attack="0" burn_defend="0" price1="1000" price2="2000" />
						</item_template>
						<item_template id="182400001" name="Kinah" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="0"/>
						<item_template id="100" name="Test Decompose Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<decompose/>
							</actions>
						</item_template>
						<item_template id="{selectableFixture.SourceItemId}" name="Test Selectable Decompose Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<decompose/>
							</actions>
						</item_template>
						<item_template id="102" name="Test Special Decompose Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<decompose/>
							</actions>
						</item_template>
						<item_template id="103" name="Test Assembly Tool" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<assemble item="188053996"/>
							</actions>
						</item_template>
						<item_template id="104" name="Test XP Extraction Tool" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<expextract item_id="188053996" cost="10" percent="false"/>
							</actions>
						</item_template>
						<item_template id="105" name="Test Extraction Tool" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<extract/>
							</actions>
						</item_template>
						<item_template id="100000500" name="Test Mythic Extraction Sword" level="65" mask="65536" item_group="SWORD" item_type="NORMAL" quality="MYTHIC" race="PC_ALL" max_stack_count="1"/>
						{extractionRewardTemplateXml}
						<item_template id="200" name="Test Decompose Reward" level="1" mask="123" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="999900202" name="Test Restricted Warehouse Reward" level="1" mask="115" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="{selectableFixture.RewardIndex0ItemId}" name="Test Selectable Reward 1" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="{selectableFixture.RewardIndex1ItemId}" name="Test Selectable Reward 2" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="188053996" name="Restricted Assembly Reward" level="1" mask="123" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="204" name="Test Special Decompose Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<inventory id="2"/>
						</item_template>
						<item_template id="205" name="Test Special Cube Filler" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<inventory id="2"/>
						</item_template>
					</item_templates>
					<decomposable_items>
						<decomposable item_id="100">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="200" min_count="1" max_count="1"/>
							</items>
						</decomposable>
						<decomposable item_id="102">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="204" min_count="1" max_count="1"/>
							</items>
						</decomposable>
						<decomposable item_id="{selectableFixture.SourceItemId}" selectable="true">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="{selectableFixture.RewardIndex0ItemId}" min_count="{selectableFixture.RewardIndex0Count}" max_count="{selectableFixture.RewardIndex0Count}" race="ELYOS" player_classes="RANGER"/>
								<item id="{selectableFixture.RewardIndex1ItemId}" min_count="{selectableFixture.RewardIndex1Count}" max_count="{selectableFixture.RewardIndex1Count}"/>
								<item id="203" min_count="1" max_count="1" race="ASMODIANS"/>
							</items>
						</decomposable>
					</decomposable_items>
					<assembly_items>
						<item id="188053996" parts="100 101"/>
					</assembly_items>
					<item_restriction_cleanups>
						<cleanup id="152200001" awh="0" lwh="0"/>
						<cleanup id="169630000" awh="0" lwh="0"/>
						<cleanup id="169640000" awh="0" lwh="0"/>
						<cleanup id="100" awh="0" lwh="0"/>
						<cleanup id="{selectableFixture.SourceItemId}" awh="0" lwh="0"/>
						<cleanup id="104" awh="0" lwh="0"/>
						<cleanup id="105" awh="0" lwh="0"/>
						<cleanup id="165005000" awh="0" lwh="0"/>
						<cleanup id="165010000" awh="0" lwh="0"/>
						<cleanup id="166000020" awh="0" lwh="0"/>
						<cleanup id="166000030" awh="0" lwh="0"/>
						<cleanup id="166000080" awh="0" lwh="0"/>
						<cleanup id="166000085" awh="0" lwh="0"/>
						<cleanup id="200" awh="0" lwh="0"/>
						<cleanup id="101" awh="0" lwh="0"/>
						<cleanup id="188053996" awh="0" lwh="0"/>
						<cleanup id="166000195" awh="0" lwh="0"/>
					</item_restriction_cleanups>
					<recipe_templates>
						<recipe_template id="155000001" nameid="730278" skillid="40009" race="ELYOS" skillpoint="1" dp="200" autolearn="1" productid="152000401" quantity="3"/>
					</recipe_templates>
					<skill_templates>
						<skill_template skill_id="1" name="Test Skill" nameId="1" lvl="1" group="" stack="" skilltype="MAGICAL" skillsubtype="NONE" cooldownId="0" cooldown="0" activation="ACTIVE"/>
					</skill_templates>
					<skill_tree>
						<skill classId="RANGER" skillId="1" race="PC_ALL" minLevel="1" autolearn="false" stigma="0"/>
					</skill_tree>
					<player_titles>
						<title id="269" nameId="1101268" desc="Test Title" race="ELYOS"/>
					</player_titles>
					<quests>
						<quest id="1114" name="Test Quest Starter Quest" minlevel_permitted="0" race_permitted="PC_ALL"/>
						<quest id="1115" name="Test Repeat Quest Starter Quest" minlevel_permitted="0" race_permitted="PC_ALL" max_repeat_count="2"/>
						<quest id="1116" name="Test Race Quest" minlevel_permitted="0" race_permitted="ASMODIANS"/>
						<quest id="1117" name="Test Min Level Quest" minlevel_permitted="10" race_permitted="PC_ALL"/>
						<quest id="1118" name="Test Max Level Quest" minlevel_permitted="0" maxlevel_permitted="2" race_permitted="PC_ALL"/>
						<quest id="1119" name="Test Class Quest" minlevel_permitted="0" race_permitted="PC_ALL">
							<class_permitted>CLERIC</class_permitted>
						</quest>
						<quest id="1120" name="Test Gender Quest" minlevel_permitted="0" race_permitted="PC_ALL">
							<gender_permitted>FEMALE</gender_permitted>
						</quest>
						<quest id="1121" name="Existing Normal Quest" minlevel_permitted="0" race_permitted="PC_ALL"/>
						<quest id="1122" name="Test No Count Quest" minlevel_permitted="0" race_permitted="PC_ALL" category="EVENT"/>
						<quest id="1123" name="Test Inventory Quest" minlevel_permitted="0" race_permitted="PC_ALL">
							<inventory_items>
								<inventory_item item_id="182215001" count="1"/>
							</inventory_items>
						</quest>
						<quest id="1124" name="Test Combine Skill Quest" minlevel_permitted="0" race_permitted="PC_ALL" combineskill="40001" combine_skillpoint="199"/>
						<quest id="1125" name="Test Rank Quest" minlevel_permitted="0" race_permitted="PC_ALL" rank="4"/>
					</quests>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager(dataManager);
			var sentPackets = new List<GameServerPacket>();
			var world = new Aion.GameServer.World.World(NullLogger<Aion.GameServer.World.World>.Instance);
			world.Initialize();
			var playerEnterWorldService = repository == null
				? null
				: new PlayerEnterWorldService(
					options,
					repository,
					world,
					NullLogger<PlayerEnterWorldService>.Instance);
			var threadPoolManager = includeThreadPoolManager
				? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance)
				: null;

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
				if (enableCryptKeyBeforeRun)
					crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"inventory-expansion-use-item-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options,
					runtimeContext: runtimeContext,
					playerEnterWorldService: playerEnterWorldService,
					threadPoolManager: threadPoolManager,
					idFactory: idFactory,
					isShuttingDownSoon: isShuttingDownSoon,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new InventoryExpansionUseItemFixture(client, connection, threadPoolManager, sentPackets, dataManager.StaticData, tempRoot);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async Task<byte[]> ReadServerFrameAsync()
		{
			var stream = _client.GetStream();
			var header = await ReadExactAsync(stream, 2);
			var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
			var payload = await ReadExactAsync(stream, length - 2);
			var frame = new byte[length];
			header.CopyTo(frame, 0);
			payload.CopyTo(frame.AsSpan(2));
			return frame;
		}

		public async Task WriteClientFrameAsync(byte[] frame)
		{
			var stream = _client.GetStream();
			await stream.WriteAsync(frame);
			await stream.FlushAsync();
		}

		private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			var buffer = new byte[length];
			var offset = 0;
			while (offset < length)
			{
				var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cts.Token);
				if (read == 0)
					throw new EndOfStreamException("Socket closed before the expected frame was read.");
				offset += read;
			}

			return buffer;
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			if (_threadPoolManager != null)
				await _threadPoolManager.DisposeAsync();
			_client.Dispose();
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
