using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public class ItemSocketServiceTests
{
	private const int KinahItemId = 182400001;
	private const int SwordItemId = 100;
	private const int ManastoneItemId = 167000001;
	private const int FusionStoneItemId = 167000002;

	[Fact]
	public void RemoveManastone_RemovesNormalStoneAndChargesKinah()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1, ItemId = KinahItemId, Count = 1_000, Location = 0 },
			CreateSword(1001, manaStones: [new ItemStoneSocket(ManastoneItemId, 0), new ItemStoneSocket(ManastoneItemId, 2)]),
		];

		var plan = ItemSocketService.CreateRemoveManastonePlan(player, 1001, slotNumber: 2, isFusionSocket: false, CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.Equal(0, plan.RemovedCategory);
		Assert.Equal(2, plan.RemovedSlot);
		Assert.Equal(350, plan.KinahItemUpdate?.Count);
		var itemUpdate = plan.ItemUpdate;
		Assert.NotNull(itemUpdate);
		Assert.Equal([0], itemUpdate.ManaStones.Select(stone => stone.Slot).ToArray());
		Assert.Equal([0], plan.InventoryItems.Single(item => item.ObjectId == 1001).ManaStones.Select(stone => stone.Slot).ToArray());
	}

	[Fact]
	public void RemoveManastone_RemovesFusionStoneWhenRequested()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1, ItemId = KinahItemId, Count = 1_000, Location = 0 },
			CreateSword(1001, fusionStones: [new ItemStoneSocket(FusionStoneItemId, 1), new ItemStoneSocket(FusionStoneItemId, 3)]),
		];

		var plan = ItemSocketService.CreateRemoveManastonePlan(player, 1001, slotNumber: 1, isFusionSocket: true, CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.Equal(2, plan.RemovedCategory);
		var itemUpdate = plan.ItemUpdate;
		Assert.NotNull(itemUpdate);
		Assert.Equal([3], itemUpdate.FusionStones.Select(stone => stone.Slot).ToArray());
	}

	[Fact]
	public void RemoveManastone_ReturnsJavaShapedFailures()
	{
		var templates = CreateItemTemplates();
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1, ItemId = KinahItemId, Count = 10, Location = 0 },
			CreateSword(1001, manaStones: [new ItemStoneSocket(ManastoneItemId, 0)]),
			CreateSword(1002),
		];

		Assert.Equal(
			ManastoneRemovalFailure.NoTargetItem,
			ItemSocketService.CreateRemoveManastonePlan(player, 404, 0, false, templates).Failure);
		Assert.Equal(
			ManastoneRemovalFailure.NoOptionToRemove,
			ItemSocketService.CreateRemoveManastonePlan(player, 1002, 0, false, templates).Failure);
		Assert.Equal(
			ManastoneRemovalFailure.InvalidSlot,
			ItemSocketService.CreateRemoveManastonePlan(player, 1001, 3, false, templates).Failure);
		Assert.Equal(
			ManastoneRemovalFailure.NotEnoughKinah,
			ItemSocketService.CreateRemoveManastonePlan(player, 1001, 0, false, templates).Failure);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 7001,
			Name = "SocketTester",
			Race = "ELYOS",
			PlayerClass = "WARRIOR",
			Gender = "MALE",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static InventoryItem CreateSword(
		int objectId,
		IReadOnlyList<ItemStoneSocket>? manaStones = null,
		IReadOnlyList<ItemStoneSocket>? fusionStones = null)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = SwordItemId,
			Count = 1,
			Location = 0,
			Slot = 10,
			ManaStones = manaStones ?? Array.Empty<ItemStoneSocket>(),
			FusionStones = fusionStones ?? Array.Empty<ItemStoneSocket>(),
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(KinahItemId, "Kinah", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(SwordItemId, "Practice Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1),
			new ItemTemplateSummary(ManastoneItemId, "Manastone: HP +20", 0, 0, 1, "MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(FusionStoneItemId, "Fusion Manastone: HP +20", 0, 0, 1, "MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
		]);
	}
}
