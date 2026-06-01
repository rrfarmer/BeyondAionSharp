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
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionStorageExpansionDialogTests
{
	[Theory]
	[InlineData(CmDialogSelect.ExtendInventory, InventoryExpansionStorage.Cube, 1000)]
	[InlineData(CmDialogSelect.ExtendCharWarehouse, InventoryExpansionStorage.Warehouse, 1200)]
	public async Task HandleDialogSelectAsync_StorageExpansionActionsRegisterJavaWarningQuestion(
		int dialogActionId,
		InventoryExpansionStorage expectedStorage,
		int expectedPrice)
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9001);
		var npc = CreateExpansionNpc(9001, templateId: expectedStorage == InventoryExpansionStorage.Cube ? 798008 : 203199, dialogActionId);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, dialogActionId));

		Assert.NotNull(player.PendingStorageExpansionRequest);
		Assert.Equal(expectedStorage, player.PendingStorageExpansionRequest!.Storage);
		Assert.Equal(npc.ObjectId, player.PendingStorageExpansionRequest.NpcObjectId);
		Assert.Equal(expectedPrice, player.PendingStorageExpansionRequest.Price);
		Assert.Equal(SmQuestionWindow.WarehouseExpandWarning, player.PendingStorageExpansionRequest.QuestionId);
		Assert.Equal(1, player.ResponseRequester.Count);
		var question = Assert.IsType<SmQuestionWindow>(Assert.Single(fixture.SentPackets));
		Assert.Equal(SmQuestionWindow.WarehouseExpandWarning, question.Code);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_StorageExpansionRejectsUnsupportedNpcAction()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9001);
		var npc = CreateExpansionNpc(9001, templateId: 798008, CmDialogSelect.CombineTask);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(
			player,
			CreateDialogSelect(npc.ObjectId, CmDialogSelect.ExtendInventory));

		Assert.Null(player.PendingStorageExpansionRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyTradeListRemainsDisabledAtSocketBoundaryUntilRoutingReady()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9001);
		var npc = CreateExpansionNpc(9001, templateId: 203060, dialogActionId: CmDialogSelect.Buy);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.Buy));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, plan.ControllerDispatchPlan?.Status);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, plan.ControllerDispatchPlan?.DialogServicePlan?.Status);
		var runtimeFacts = Assert.IsType<NpcDialogTradeRuntimeFactAdapterPlan>(plan.TradeRuntimeFactPlan);
		Assert.Equal(player.ObjectId, runtimeFacts.PlayerObjectId);
		Assert.Equal(0, runtimeFacts.PlayerLegionLevel);
		Assert.Equal(100, runtimeFacts.VendorBuyModifier);
		Assert.Contains("Staged default", runtimeFacts.LegionLevelSource);
		Assert.Equal("Injected runtime value", runtimeFacts.VendorBuyModifierSource);
		Assert.False(runtimeFacts.IsLive);
		var packetPlan = Assert.IsType<SmTradeListPacketPlan>(plan.TradeListPacketPlan);
		Assert.Equal(SmTradeListPacketPlanStatus.Ready, packetPlan.Status);
		Assert.Equal([129], packetPlan.TradeTabIds);
		Assert.Equal(
			[
				new SmTradeListLimitedItemSummary(186000001, BuyCount: 0, SellLimit: 5),
			],
			packetPlan.LimitedItems);
		Assert.Equal(80, packetPlan.BuyPriceModifier);
		Assert.False(packetPlan.IsLive);
		var descriptor = Assert.Single(plan.ControllerDispatchPlan!.DialogServicePlan!.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeListPacket, descriptor.Kind);
		Assert.Same(packetPlan, descriptor.TradeListPacketPlan);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyNoTradeListPlansNoSellMessageWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9002);
		var npc = CreateExpansionNpc(9002, templateId: 203061, dialogActionId: CmDialogSelect.Buy);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.Buy));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, plan.ControllerDispatchPlan?.Status);
		var tradeListFacts = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.False(tradeListFacts.Facts.HasTradeList);
		Assert.False(tradeListFacts.Facts.HasSellableTradeGoods);
		Assert.Null(plan.TradeListPacketPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyUnavailable, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, descriptor.Kind);
		Assert.False(servicePlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyTradeListUsesConfiguredVendorBuyModifierInNonLivePlan()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync(
			new GameServerOptions
			{
				Prices = new GameServerPriceOptions
				{
					VendorBuyModifier = 125,
				},
			});
		var player = CreatePlayer(targetObjectId: 9005);
		var npc = CreateExpansionNpc(9005, templateId: 203060, dialogActionId: CmDialogSelect.Buy);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.Buy));

		Assert.Empty(fixture.SentPackets);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		var runtimeFacts = Assert.IsType<NpcDialogTradeRuntimeFactAdapterPlan>(plan.TradeRuntimeFactPlan);
		Assert.Equal(125, runtimeFacts.VendorBuyModifier);
		Assert.Equal("Injected runtime value", runtimeFacts.VendorBuyModifierSource);
		var packetPlan = Assert.IsType<SmTradeListPacketPlan>(plan.TradeListPacketPlan);
		Assert.Equal(100, packetPlan.BuyPriceModifier);
		Assert.False(packetPlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyRestrictedGoodsPlansNoSellMessageWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9003);
		var npc = CreateExpansionNpc(9003, templateId: 203062, dialogActionId: CmDialogSelect.Buy);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.Buy));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		var tradeListFacts = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.True(tradeListFacts.Facts.HasTradeList);
		Assert.False(tradeListFacts.Facts.HasSellableTradeGoods);
		Assert.Equal([130], tradeListFacts.RestrictedGoodsListIds);
		Assert.Null(plan.TradeListPacketPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyUnavailable, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, descriptor.Kind);
		Assert.False(servicePlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyRestrictedGoodsUsesHydratedLegionLevelWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9003, legionId: 77, legionLevel: 5);
		var npc = CreateExpansionNpc(9003, templateId: 203062, dialogActionId: CmDialogSelect.Buy);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.Buy));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		var runtimeFacts = Assert.IsType<NpcDialogTradeRuntimeFactAdapterPlan>(plan.TradeRuntimeFactPlan);
		Assert.Equal(77, runtimeFacts.PlayerLegionId);
		Assert.Equal(5, runtimeFacts.PlayerLegionLevel);
		Assert.Equal("Injected runtime value", runtimeFacts.LegionLevelSource);
		Assert.False(runtimeFacts.IsLive);
		var tradeListFacts = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.True(tradeListFacts.Facts.HasSellableTradeGoods);
		Assert.Empty(tradeListFacts.RestrictedGoodsListIds);
		var packetPlan = Assert.IsType<SmTradeListPacketPlan>(plan.TradeListPacketPlan);
		Assert.Equal(SmTradeListPacketPlanStatus.Ready, packetPlan.Status);
		Assert.Equal([130], packetPlan.TradeTabIds);
		Assert.False(packetPlan.IsLive);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, servicePlan.Status);
		Assert.False(servicePlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyMissingGoodsPlansNoSellMessageWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9004);
		var npc = CreateExpansionNpc(9004, templateId: 203063, dialogActionId: CmDialogSelect.Buy);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.Buy));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		var tradeListFacts = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.True(tradeListFacts.Facts.HasTradeList);
		Assert.False(tradeListFacts.Facts.HasSellableTradeGoods);
		Assert.Equal([131], tradeListFacts.MissingGoodsListIds);
		Assert.Null(plan.TradeListPacketPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyUnavailable, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, descriptor.Kind);
		Assert.False(servicePlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_TradeInListRemainsDisabledAtSocketBoundaryUntilRoutingReady()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9006);
		var npc = CreateExpansionNpc(9006, templateId: 205315, dialogActionId: CmDialogSelect.TradeIn);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.TradeIn));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, plan.ControllerDispatchPlan?.Status);
		Assert.Equal(NpcDialogServiceSelectStatus.TradeInList, plan.ControllerDispatchPlan?.DialogServicePlan?.Status);
		var tradeListFacts = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.True(tradeListFacts.Facts.HasTradeInList);
		Assert.Null(plan.TradeListPacketPlan);
		var packetPlan = Assert.IsType<SmTradeInListPacketPlan>(plan.TradeInListPacketPlan);
		Assert.Equal(SmTradeInListPacketPlanStatus.Ready, packetPlan.Status);
		Assert.Equal(205315, packetPlan.NpcId);
		Assert.Equal(1, packetPlan.TradeNpcTypeIndex);
		Assert.Equal([39], packetPlan.TradeTabIds);
		Assert.Equal(100, packetPlan.BuyPriceModifier);
		Assert.False(packetPlan.IsLive);
		var descriptor = Assert.Single(plan.ControllerDispatchPlan!.DialogServicePlan!.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeInListPacket, descriptor.Kind);
		Assert.Same(packetPlan, descriptor.TradeInListPacketPlan);
		Assert.Equal(100, descriptor.PriceModifier);
		Assert.False(plan.ControllerDispatchPlan.DialogServicePlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_TradeInNoTradeListPlansNoSellMessageWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9007);
		var npc = CreateExpansionNpc(9007, templateId: 205316, dialogActionId: CmDialogSelect.TradeIn);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.TradeIn));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, plan.ControllerDispatchPlan?.Status);
		var tradeListFacts = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.False(tradeListFacts.Facts.HasTradeInList);
		Assert.Null(plan.TradeListPacketPlan);
		Assert.Null(plan.TradeInListPacketPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.TradeInUnavailable, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, descriptor.Kind);
		Assert.Null(descriptor.TradeInListPacketPlan);
		Assert.False(servicePlan.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyAgainPlansRepurchasePacketWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9008);
		var npc = CreateExpansionNpc(9008, templateId: 203064, dialogActionId: CmDialogSelect.BuyAgain);
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.BuyAgain));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(0, player.ResponseRequester.Count);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.NotNull(plan.RepurchasePacket);
		var packetSnapshotPlan = Assert.IsType<RepurchasePacketSnapshotPlan>(plan.RepurchasePacketSnapshotPlan);
		Assert.Equal(RepurchasePacketSnapshotPlanStatus.SnapshotCreated, packetSnapshotPlan.Status);
		Assert.True(packetSnapshotPlan.WouldQueryRepurchaseItems);
		Assert.False(packetSnapshotPlan.DidQueryRepurchaseItems);
		Assert.True(packetSnapshotPlan.WouldSendPacket);
		Assert.False(packetSnapshotPlan.DidSendPacket);
		Assert.False(packetSnapshotPlan.IsLive);
		Assert.Same(packetSnapshotPlan.Packet, plan.RepurchasePacket);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.ServiceDispatch, servicePlan.Status);
		Assert.Contains("BUY_AGAIN", servicePlan.JavaSource, StringComparison.Ordinal);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.RepurchasePacket, descriptor.Kind);
		Assert.Equal(npc.ObjectId, descriptor.TargetObjectId);
		Assert.Same(packetSnapshotPlan, descriptor.RepurchasePacketSnapshotPlan);
		Assert.Same(plan.RepurchasePacket, descriptor.RepurchasePacket);
		Assert.False(servicePlan.IsLive);
		Assert.False(descriptor.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyAgainCarriesMissingTemplateSnapshotWithoutFallbackPacket()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9010);
		var npc = CreateExpansionNpc(9010, templateId: 203064, dialogActionId: CmDialogSelect.BuyAgain);
		var missingTemplateItem = new InventoryItem
		{
			ObjectId = 7003,
			ItemId = MissingTemplateItemId,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = 65535,
		};
		player.RepurchaseItems = [new RepurchaseSourceItem(missingTemplateItem, RepurchasePrice: 1_234)];
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.BuyAgain));

		Assert.Empty(fixture.SentPackets);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		Assert.Null(plan.RepurchasePacket);
		var packetSnapshotPlan = Assert.IsType<RepurchasePacketSnapshotPlan>(plan.RepurchasePacketSnapshotPlan);
		Assert.Equal(RepurchasePacketSnapshotPlanStatus.BlockedMissingTemplate, packetSnapshotPlan.Status);
		Assert.Equal([MissingTemplateItemId], packetSnapshotPlan.MissingTemplateItemIds);
		Assert.Null(packetSnapshotPlan.Packet);
		Assert.True(packetSnapshotPlan.WouldQueryRepurchaseItems);
		Assert.False(packetSnapshotPlan.WouldSendPacket);
		Assert.Contains("lack item templates", packetSnapshotPlan.JavaSource, StringComparison.Ordinal);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.RepurchasePacket, descriptor.Kind);
		Assert.Same(packetSnapshotPlan, descriptor.RepurchasePacketSnapshotPlan);
		Assert.Null(descriptor.RepurchasePacket);
		Assert.False(descriptor.IsLive);
	}

	[Fact]
	public async Task HandleDialogSelectAsync_BuyAgainComposesRepurchaseSnapshotPacketWithoutSending()
	{
		await using var fixture = await StorageExpansionDialogFixture.CreateAsync();
		var player = CreatePlayer(targetObjectId: 9009);
		var npc = CreateExpansionNpc(9009, templateId: 203064, dialogActionId: CmDialogSelect.BuyAgain);
		var soldItem = new InventoryItem
		{
			ObjectId = 7001,
			ItemId = SwordItemId,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = 65535,
		};
		var sellPlan = TradeSellToShopPlanService.CreatePlan(
			canTrade: true,
			player,
			inventoryItems: [soldItem],
			tradeItems: [new TradeSellToShopItemRequest(soldItem.ObjectId, Count: 1)],
			CreateItemTemplates(),
			purchaseTemplate: null,
			goodsLists: null,
			sellModifier: 20,
			nextObjectId: () => 8001);
		var snapshot = RepurchaseDiagnosticSnapshotPlanService.CreateDisabledPlan(sellPlan);
		player.RepurchaseItems = snapshot.RepurchaseItems;
		fixture.World.TryAddObject(npc.ObjectId, npc);

		await fixture.Connection.HandleDialogSelectAsync(player, CreateDialogSelect(npc.ObjectId, CmDialogSelect.BuyAgain));

		Assert.Empty(fixture.SentPackets);
		Assert.Equal(RepurchaseDiagnosticSnapshotPlanStatus.SnapshotCreated, snapshot.Status);
		Assert.True(snapshot.WouldReplacePlayerSnapshot);
		Assert.False(snapshot.DidReplacePlayerSnapshot);
		var plan = Assert.Single(fixture.DialogSelectPlans);
		var packetSnapshotPlan = Assert.IsType<RepurchasePacketSnapshotPlan>(plan.RepurchasePacketSnapshotPlan);
		Assert.Equal(RepurchasePacketSnapshotPlanStatus.SnapshotCreated, packetSnapshotPlan.Status);
		Assert.Equal(npc.ObjectId, packetSnapshotPlan.TargetObjectId);
		Assert.Single(packetSnapshotPlan.RepurchaseItems);
		Assert.Empty(packetSnapshotPlan.MissingTemplateItemIds);
		Assert.True(packetSnapshotPlan.WouldQueryRepurchaseItems);
		Assert.False(packetSnapshotPlan.DidQueryRepurchaseItems);
		Assert.True(packetSnapshotPlan.WouldSendPacket);
		Assert.False(packetSnapshotPlan.DidSendPacket);
		Assert.False(packetSnapshotPlan.ShouldDispatchLiveSideEffects);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.ControllerDispatchPlan?.DialogServicePlan);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.RepurchasePacket, descriptor.Kind);
		Assert.Same(packetSnapshotPlan, descriptor.RepurchasePacketSnapshotPlan);
		Assert.Same(packetSnapshotPlan.Packet, descriptor.RepurchasePacket);
		Assert.Same(plan.RepurchasePacket, descriptor.RepurchasePacket);

		var payload = SerializeUnencryptedPayload(descriptor.RepurchasePacket!);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(npc.ObjectId, reader.ReadD());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.True(reader.Remaining > 0);
	}

	private static Player CreatePlayer(int targetObjectId, int legionId = 0, int legionLevel = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "ExpansionTester",
			TargetObjectId = targetObjectId,
			LegionId = legionId,
			LegionLevel = legionLevel,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static WorldNpc CreateExpansionNpc(int objectId, int templateId, int dialogActionId)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Expansion Master",
			123456,
			1,
			"NORMAL",
			"NORMAL",
			"PC_ALL",
			string.Empty,
			"NPC",
			FunctionDialogIds: [dialogActionId],
			HasTalkInfo: true,
			IsDialogNpc: true);
		return new WorldNpc(objectId, templateId, template, new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static CmDialogSelect CreateDialogSelect(int targetObjectId, int dialogActionId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(targetObjectId);
		writer.WriteH(dialogActionId);
		writer.WriteH(0);
		writer.WriteH(0);
		writer.WriteD(0);
		writer.WriteH(0);
		var packet = new CmDialogSelect(56, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable([CreateItemTemplate(SwordItemId, price: 1_000)]);
	}

	private static ItemTemplateSummary CreateItemTemplate(int itemId, long price)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: price,
			ValidEquipmentSlots: 0);
	}

	private const int SwordItemId = 100000001;
	private const int MissingTemplateItemId = 100000099;

	private sealed class StorageExpansionDialogFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly string _tempRoot;

		private StorageExpansionDialogFixture(
			TcpClient client,
			GameServerConnection connection,
			GameWorld world,
			List<GameServerPacket> sentPackets,
			List<QuestDialogNpcTargetBranchInputAssemblyPlan> dialogSelectPlans,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			World = world;
			SentPackets = sentPackets;
			DialogSelectPlans = dialogSelectPlans;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public GameWorld World { get; }

		public List<GameServerPacket> SentPackets { get; }

		public List<QuestDialogNpcTargetBranchInputAssemblyPlan> DialogSelectPlans { get; }

		public static async Task<StorageExpansionDialogFixture> CreateAsync(GameServerOptions? options = null)
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-storage-dialog-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<item_templates>
						<item_template id="100000001" name="Item 100000001" desc="1" mask="0" level="1" item_group="NORMAL" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1" price="1000" />
					</item_templates>
					<cube_expander>
						<expansion_npc ids="798008">
							<expand level="1" price="1000" />
						</expansion_npc>
					</cube_expander>
					<warehouse_expander>
						<expansion_npc ids="203199">
							<expand level="1" price="1200" />
						</expansion_npc>
					</warehouse_expander>
					<npc_trade_list>
						<tradelist_template npc_id="203060" npc_type="NORMAL" sell_price_rate="80">
							<tradelist id="129" />
						</tradelist_template>
						<tradelist_template npc_id="203062" npc_type="NORMAL" sell_price_rate="80">
							<tradelist id="130" />
						</tradelist_template>
						<tradelist_template npc_id="203063" npc_type="NORMAL" sell_price_rate="80">
							<tradelist id="131" />
						</tradelist_template>
						<trade_in_list_template npc_id="205315" npc_type="NORMAL">
							<tradelist id="39" />
						</trade_in_list_template>
					</npc_trade_list>
					<goodslists>
						<list id="129">
							<item id="110100010" />
							<item id="186000001" sell_limit="5" buy_limit="3" />
						</list>
						<list id="130" legion_lvl="5">
							<item id="110100011" />
						</list>
					</goodslists>
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
			var sentPackets = new List<GameServerPacket>();
			var dialogSelectPlans = new List<QuestDialogNpcTargetBranchInputAssemblyPlan>();

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
					"storage-expansion-dialog-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options ?? new GameServerOptions(),
					runtimeContext: runtimeContext,
					world: world,
					sentPacketObserver: sentPackets.Add,
					dialogSelectPlanObserver: dialogSelectPlans.Add,
					crypt: crypt);
				return new StorageExpansionDialogFixture(client, connection, world, sentPackets, dialogSelectPlans, tempRoot);
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
}
