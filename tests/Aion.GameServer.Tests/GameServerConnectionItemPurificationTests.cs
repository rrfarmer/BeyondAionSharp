using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionItemPurificationTests
{
	[Fact]
	public async Task HandleItemPurificationAsync_UsesActivePlayerBaseItemAndIgnoresPacketMaterialObjectIdsWithoutMutation()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(handlerPlan);
		Assert.True(handlerPlan.Workflow.Succeeded);
		Assert.Equal(ItemPurificationApStatus.Allowed, handlerPlan.Workflow.Validation?.Status);
		Assert.Equal([20, 10], handlerPlan.Workflow.MaterialMutation?.DeletedObjectIds);
		Assert.Equal(1_200, handlerPlan.Workflow.MaterialMutation?.AbyssPointsToSpend);
		Assert.Equal(0, handlerPlan.Workflow.Inheritance?.TargetItem?.ObjectId);
		Assert.Equal(100000002, handlerPlan.Workflow.Inheritance?.TargetItem?.ItemId);
		Assert.Equal(ItemPurificationApplicationPlanStatus.NeedsTargetObjectIdAllocation, handlerPlan.Application.Status);
		Assert.Equal(ItemPurificationPacketPlanStatus.NeedsRuntimeInputs, handlerPlan.PacketPlan.Status);
		Assert.Equal(5_000, player.AbyssRank.Ap);
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(2, material.Count);
		Assert.Equal(10_000, kinah.Count);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_AllocatesTargetObjectIdWhenFactoryAvailable()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		var idFactory = new IDFactory(Enumerable.Range(1, 9000));
		await using var pair = await TestConnectionPair.CreateAsync(idFactory);
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(handlerPlan);
		Assert.True(handlerPlan.Workflow.Succeeded);
		Assert.True(handlerPlan.Application.Succeeded);
		Assert.Equal(9001, handlerPlan.Application.TargetItem?.ObjectId);
		Assert.Equal(9001, handlerPlan.Workflow.Inheritance?.TargetItem?.ObjectId);
		Assert.False(handlerPlan.Application.RequiresTargetObjectIdAllocation);
		Assert.True(handlerPlan.PacketPlan.Succeeded);
		Assert.Contains(handlerPlan.PacketPlan.Operations, operation =>
			operation.Type == ItemPurificationPacketOperationType.InventoryAddItem
			&& operation.ObjectId == 9001
			&& operation.ItemId == 100000002);
		Assert.Equal(9002, idFactory.GetUsedCount());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(2, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_DoesNotAllocateWhenRandomBonusSelectionIsPending()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		var idFactory = new IDFactory(Enumerable.Range(1, 9000));
		await using var pair = await TestConnectionPair.CreateAsync(idFactory);
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates(targetStatBonusSetId: 2));

		Assert.NotNull(handlerPlan);
		Assert.Equal(ItemPurificationApplicationPlanStatus.NeedsTargetObjectIdAllocation, handlerPlan.Application.Status);
		Assert.True(handlerPlan.Application.RequiresTargetObjectIdAllocation);
		Assert.True(handlerPlan.Application.RequiresRandomBonusSelection);
		Assert.Equal(0, handlerPlan.Application.TargetItem?.ObjectId);
		Assert.Equal(9001, idFactory.NextId());
	}

	[Fact]
	public async Task HandleItemPurificationAsync_SelectsRandomBonusAndAllocatesWhenBonusTableAvailable()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		var idFactory = new IDFactory(Enumerable.Range(1, 9000));
		await using var pair = await TestConnectionPair.CreateAsync(idFactory);
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates(targetStatBonusSetId: 2),
			itemRandomBonusesOverride: CreateRandomBonuses(set1GroupCount: 1, set2GroupCount: 2),
			randomBonusRoll: () => 0.75d);

		Assert.NotNull(handlerPlan);
		Assert.True(handlerPlan.Application.Succeeded);
		Assert.Equal(9001, handlerPlan.Application.TargetItem?.ObjectId);
		Assert.Equal(2, handlerPlan.Application.TargetItem?.RandomBonus);
		Assert.True(handlerPlan.Workflow.Inheritance?.RandomBonusWasRerolled);
		Assert.False(handlerPlan.Application.RequiresRandomBonusSelection);
		Assert.True(handlerPlan.PacketPlan.Succeeded);
		Assert.Equal(9002, idFactory.GetUsedCount());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_ReleasesAllocatedTargetObjectIdWhenRebuiltPlanIsNotReady()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, kinah],
		};
		var idFactory = new IDFactory(Enumerable.Range(1, 9000));
		await using var pair = await TestConnectionPair.CreateAsync(idFactory);
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(requiredMaterialItemId: baseItem.ItemId, requiredMaterialCount: 1),
			CreateItemTemplates());

		Assert.NotNull(handlerPlan);
		Assert.Equal(ItemPurificationApplicationPlanStatus.NeedsBaseItemDeleteVerification, handlerPlan.Application.Status);
		Assert.Equal(9001, handlerPlan.Application.TargetItem?.ObjectId);
		Assert.Equal(9001, idFactory.NextId());
		Assert.Equal([10, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_ReturnsMissingBaseItemPlanWithoutThrowing()
	{
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 }],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket(
			playerObjectId: player.ObjectId,
			baseItemObjectId: 999,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [0, 0, 0, 0, 0]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(handlerPlan);
		Assert.Equal(ItemPurificationWorkflowStatus.MissingBaseItem, handlerPlan.Workflow.Status);
		Assert.Null(handlerPlan.Workflow.Validation);
		Assert.Null(handlerPlan.Workflow.MaterialMutation);
		Assert.Null(handlerPlan.Workflow.Inheritance);
		Assert.Equal(ItemPurificationApplicationPlanStatus.WorkflowNotPlanned, handlerPlan.Application.Status);
		Assert.Equal(ItemPurificationPacketPlanStatus.ApplicationPlanUnavailable, handlerPlan.PacketPlan.Status);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_ComposesApplicationAndPacketPlansWhenTargetObjectIdProvided()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates(),
			targetObjectId: 9001);

		Assert.NotNull(handlerPlan);
		Assert.True(handlerPlan.Workflow.Succeeded);
		Assert.True(handlerPlan.Application.Succeeded);
		Assert.Equal(9001, handlerPlan.Application.TargetItem?.ObjectId);
		Assert.Equal(20, handlerPlan.Application.TargetItem?.Enchant);
		Assert.Equal(
			[
				ItemPurificationApplicationOperationType.DeleteMaterialItem,
				ItemPurificationApplicationOperationType.SpendAbyssPoints,
				ItemPurificationApplicationOperationType.PreserveKinahNoOp,
				ItemPurificationApplicationOperationType.DeleteBaseItem,
				ItemPurificationApplicationOperationType.AddTargetItem,
			],
			handlerPlan.Application.Operations.Select(operation => operation.Type).ToArray());
		Assert.True(handlerPlan.PacketPlan.Succeeded);
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.UpgradeSuccessSystemMessage,
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.InventoryAddItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
			],
			handlerPlan.PacketPlan.Operations.Select(operation => operation.Type).ToArray());
		Assert.Equal(["item-100000001", "item-100000002"], handlerPlan.PacketPlan.Operations[0].Parameters);
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task ItemPurificationHandlerPacketBridge_ComposesConcretePacketsFromPostMutationSnapshots()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var itemTemplates = CreateItemTemplates();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);
		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			itemTemplates,
			targetObjectId: 9001);
		var postMutationItems = new[]
		{
			kinah,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
		};

		var bridge = ItemPurificationHandlerPacketBridgeService.CreateConcretePacketPlan(
			handlerPlan,
			postMutationItems,
			itemTemplates,
			new Dictionary<int, ItemPurificationCubeSnapshot>
			{
				[5] = new(ItemsCount: 2, NpcExpands: 1, QuestExpands: 0, ItemExpands: 0),
				[7] = new(ItemsCount: 3, NpcExpands: 1, QuestExpands: 0, ItemExpands: 1),
			});

		Assert.True(bridge.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.Ready, bridge.PacketInputs?.Status);
		Assert.NotNull(bridge.ConcretePacketPlan);
		var concretePlan = bridge.ConcretePacketPlan;
		Assert.True(concretePlan.Succeeded);
		Assert.Equal(["item-100000001", "item-100000002"], concretePlan.Operations[0].Parameters);
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			concretePlan.Operations
				.Where(operation => operation.ConcretePacket != null)
				.Select(operation => operation.ConcretePacket!.GetType())
				.ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			concretePlan.Operations
				.Where(operation => operation.ConcretePacket == null)
				.Select(operation => operation.Type)
				.ToArray());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task ItemPurificationHandlerMutationBridge_ComposesConcretePacketsFromCurrentInventoryPreview()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var itemTemplates = CreateItemTemplates();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);
		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			itemTemplates,
			targetObjectId: 9001);

		var mutationBridge = ItemPurificationHandlerPacketBridgeService.CreateConcretePacketPlanFromCurrentInventory(
			handlerPlan,
			player.InventoryItems,
			itemTemplates,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1);

		Assert.True(mutationBridge.Succeeded);
		Assert.Equal(ItemPurificationHandlerMutationBridgeStatus.Ready, mutationBridge.Status);
		Assert.NotNull(mutationBridge.MutationPreview);
		Assert.True(mutationBridge.MutationPreview.Succeeded);
		Assert.Equal([20, 30, 9001], mutationBridge.MutationPreview.PostMutationInventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.Equal(1, mutationBridge.MutationPreview.CubeSnapshotsByPacketOperationIndex[5].ItemsCount);
		Assert.Equal(2, mutationBridge.MutationPreview.CubeSnapshotsByPacketOperationIndex[7].ItemsCount);
		Assert.NotNull(mutationBridge.Bridge);
		Assert.True(mutationBridge.Bridge.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.Ready, mutationBridge.Bridge.PacketInputs?.Status);
		var concretePlan = mutationBridge.Bridge.ConcretePacketPlan;
		Assert.NotNull(concretePlan);
		Assert.True(concretePlan.Succeeded);
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			concretePlan.Operations
				.Where(operation => operation.ConcretePacket != null)
				.Select(operation => operation.ConcretePacket!.GetType())
				.ToArray());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(10_000, kinah.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task ItemPurificationHandlerMutationBridge_ReportsPreviewFailuresWithoutPacketBridge()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var itemTemplates = CreateItemTemplates();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);
		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			itemTemplates,
			targetObjectId: 9001);

		var mutationBridge = ItemPurificationHandlerPacketBridgeService.CreateConcretePacketPlanFromCurrentInventory(
			handlerPlan,
			currentInventoryItems: [baseItem, kinah],
			itemTemplates,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0);

		Assert.False(mutationBridge.Succeeded);
		Assert.Equal(ItemPurificationHandlerMutationBridgeStatus.MutationSnapshotNotReady, mutationBridge.Status);
		Assert.NotNull(mutationBridge.MutationPreview);
		Assert.Equal(ItemPurificationMutationSnapshotStatus.MissingCurrentInventoryItems, mutationBridge.MutationPreview.Status);
		Assert.Equal([20], mutationBridge.MutationPreview.MissingObjectIds);
		Assert.Null(mutationBridge.Bridge);
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task ItemPurificationHandlerPacketBridge_SendsConcretePacketsAndSkipsMetadata()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var itemTemplates = CreateItemTemplates();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);
		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			itemTemplates,
			targetObjectId: 9001);
		var postMutationItems = new[]
		{
			kinah,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
		};
		var registry = new RecordingConnectionRegistry();

		var send = await ItemPurificationHandlerPacketBridgeService.SendConcretePacketsAsync(
			player.ObjectId,
			handlerPlan,
			postMutationItems,
			itemTemplates,
			new Dictionary<int, ItemPurificationCubeSnapshot>
			{
				[5] = new(ItemsCount: 2, NpcExpands: 1, QuestExpands: 0, ItemExpands: 0),
				[7] = new(ItemsCount: 3, NpcExpands: 1, QuestExpands: 0, ItemExpands: 1),
			},
			registry);

		Assert.True(send.Succeeded);
		Assert.Equal(ItemPurificationHandlerPacketBridgeStatus.Ready, send.Bridge?.Status);
		Assert.Equal(ItemPurificationPacketSendStatus.Ready, send.SendResult?.Status);
		Assert.Equal(6, send.SendResult?.SentCount);
		Assert.Equal([player.ObjectId, player.ObjectId, player.ObjectId, player.ObjectId, player.ObjectId, player.ObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId).ToArray());
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			registry.SentPackets.Select(packet => packet.Packet.GetType()).ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			send.SendResult?.SkippedMetadataOperations.Select(operation => operation.Type).ToArray());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	private static CmItemPurification CreatePacket(
		int playerObjectId,
		int baseItemObjectId,
		int resultItemId,
		IReadOnlyList<int> requiredMaterialObjectIds)
	{
		using var body = new PacketBuffer();
		body.WriteD(playerObjectId);
		body.WriteD(baseItemObjectId);
		body.WriteD(resultItemId);
		foreach (var objectId in requiredMaterialObjectIds)
			body.WriteD(objectId);

		var packet = new CmItemPurification(247, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(body.ToArray(), strictReads: false);
		packet.ReadFrom(reader);
		return packet;
	}

	private static ItemPurificationTable CreatePurificationTable(
		int requiredMaterialItemId = 186000001,
		long requiredMaterialCount = 2)
	{
		return new ItemPurificationTable(
		[
			new ItemPurificationSummary(
				100000001,
				[
					new ItemPurificationResultSummary(
						ResultItemId: 100000002,
						MinEnchantCount: 10,
						NecessaryAbyssPoints: 1_200,
						NecessaryKinah: 1_000,
						RequiredMaterials: [new ItemPurificationMaterialSummary(requiredMaterialItemId, requiredMaterialCount)]),
				]),
		]);
	}

	private static ItemTemplateTable CreateItemTemplates(int targetStatBonusSetId = 1)
	{
		return new ItemTemplateTable(
		[
			CreateTemplate(100000001, statBonusSetId: 1, maxTuneCount: 5, maxEnchantLevel: 15),
			CreateTemplate(100000002, statBonusSetId: targetStatBonusSetId, maxTuneCount: 1, maxEnchantLevel: 20),
			CreateTemplate(186000001, statBonusSetId: 0, maxTuneCount: 0, maxEnchantLevel: 0),
		]);
	}

	private static ItemTemplateSummary CreateTemplate(
		int templateId,
		int statBonusSetId,
		int maxTuneCount,
		int maxEnchantLevel)
	{
		return new ItemTemplateSummary(
			TemplateId: templateId,
			Name: $"item-{templateId}",
			DescriptionId: 0,
			Mask: 0,
			Level: 65,
			ItemGroup: "SWORD",
			ItemType: "normal",
			Quality: "MYTHIC",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0,
			StatBonusSetId: statBonusSetId,
			MaxTuneCount: maxTuneCount,
			MaxEnchantLevel: maxEnchantLevel);
	}

	private static ItemRandomBonusTable CreateRandomBonuses(int set1GroupCount, int set2GroupCount)
	{
		return new ItemRandomBonusTable(
		[
			new ItemRandomBonusSummary("INVENTORY", 1, CreateModifierGroups(set1GroupCount), Enumerable.Repeat(1d, set1GroupCount).ToArray()),
			new ItemRandomBonusSummary("INVENTORY", 2, CreateModifierGroups(set2GroupCount), Enumerable.Repeat(1d, set2GroupCount).ToArray()),
		]);
	}

	private static IReadOnlyList<IReadOnlyList<ItemStatModifier>> CreateModifierGroups(int count)
	{
		return Enumerable.Range(1, count)
			.Select(index => (IReadOnlyList<ItemStatModifier>)[new ItemStatModifier("add", $"STAT{index}", index, Bonus: true)])
			.ToArray();
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<TestConnectionPair> CreateAsync(IDFactory? idFactory = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"item-purification-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					idFactory: idFactory);
				return new TestConnectionPair(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<SentPacketRecord> SentPackets { get; } = [];

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
			SentPackets.Add(new SentPacketRecord(playerObjectId, packet));
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
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(IReadOnlyList<WorldHouse> houses, HousingTemplateTable? housingTemplates, int? playerObjectId = null)
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

	private sealed record SentPacketRecord(int PlayerObjectId, GameServerPacket Packet);
}
