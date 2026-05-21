using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public class ItemSocketServiceTests
{
	private const int KinahItemId = 182400001;
	private const int SwordItemId = 100;
	private const int PlainSwordItemId = 101;
	private const int FusedSwordItemId = 102;
	private const int ManastoneItemId = 167000001;
	private const int FusionStoneItemId = 167000002;
	private const int SpecialManastoneItemId = 167100001;
	private const int GodstoneItemId = 168000001;
	private const int InvalidGodstoneItemId = 168000002;

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

	[Fact]
	public void SocketGodstone_AddsGodstoneAndConsumesSourceItem()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			CreateSword(1001),
			new InventoryItem { ObjectId = 2001, ItemId = GodstoneItemId, Count = 2, Location = 0 },
		];

		var plan = ItemSocketService.CreateSocketGodstonePlan(player, 1001, 2001, CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.Equal(GodstoneItemId, plan.TargetItemUpdate?.Godstone?.ItemId);
		Assert.Equal(1, plan.SourceItemUpdate?.Count);
		Assert.Null(plan.DeletedSourceItemObjectId);
		Assert.Equal(GodstoneItemId, plan.InventoryItems.Single(item => item.ObjectId == 1001).Godstone?.ItemId);
	}

	[Fact]
	public void SocketGodstone_DeletesSingleSourceItem()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			CreateSword(1001),
			new InventoryItem { ObjectId = 2001, ItemId = GodstoneItemId, Count = 1, Location = 0 },
		];

		var plan = ItemSocketService.CreateSocketGodstonePlan(player, 1001, 2001, CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.Null(plan.SourceItemUpdate);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
		Assert.DoesNotContain(plan.InventoryItems, item => item.ObjectId == 2001);
	}

	[Fact]
	public void SocketGodstone_ReturnsJavaShapedFailures()
	{
		var templates = CreateItemTemplates();
		var player = CreatePlayer();
		player.InventoryItems =
		[
			CreateSword(1001),
			new InventoryItem { ObjectId = 1003, ItemId = PlainSwordItemId, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 1004, ItemId = SwordItemId, Count = 1, Location = 0, IsEquipped = true },
			new InventoryItem { ObjectId = 2002, ItemId = InvalidGodstoneItemId, Count = 1, Location = 0 },
		];

		Assert.Equal(
			GodstoneSocketFailure.NoTargetItem,
			ItemSocketService.CreateSocketGodstonePlan(player, 404, 2001, templates).Failure);
		Assert.Equal(
			GodstoneSocketFailure.TargetItemEquipped,
			ItemSocketService.CreateSocketGodstonePlan(player, 1004, 2001, templates).Failure);
		Assert.Equal(
			GodstoneSocketFailure.TargetNotProcGivable,
			ItemSocketService.CreateSocketGodstonePlan(player, 1003, 2001, templates).Failure);
		Assert.Equal(
			GodstoneSocketFailure.NoGodstoneItem,
			ItemSocketService.CreateSocketGodstonePlan(player, 1001, 404, templates).Failure);
		Assert.Equal(
			GodstoneSocketFailure.NoGodstoneItem,
			ItemSocketService.CreateSocketGodstonePlan(player, 1001, 2002, templates).Failure);
	}

	[Fact]
	public void AddManastone_UsesJavaNormalAndSpecialSlotRanges()
	{
		var item = CreateSword(
			1001,
			manaStones:
			[
				new ItemStoneSocket(SpecialManastoneItemId, 0),
				new ItemStoneSocket(ManastoneItemId, 1),
			]);
		var templates = CreateItemTemplates();

		var normalPlan = ItemSocketService.CreateAddManastonePlan(item, ManastoneItemId, useFusionSlots: false, templates);
		var specialPlan = ItemSocketService.CreateAddManastonePlan(item, SpecialManastoneItemId, useFusionSlots: false, templates);

		Assert.True(normalPlan.Succeeded);
		Assert.Equal(2, normalPlan.AddedStone?.Slot);
		Assert.Equal(0, normalPlan.AddedCategory);
		Assert.NotNull(normalPlan.ItemUpdate);
		Assert.Equal([0, 1, 2], normalPlan.ItemUpdate.ManaStones.Select(stone => stone.Slot).ToArray());
		Assert.False(specialPlan.Succeeded);
		Assert.Equal(ManastoneAddFailure.NoAvailableSlot, specialPlan.Failure);
	}

	[Fact]
	public void AddManastone_UsesFusionTemplateSlots()
	{
		var item = CreateSword(
			1001,
			fusionStones: [new ItemStoneSocket(SpecialManastoneItemId, 0)],
			fusionedItemId: FusedSwordItemId,
			optionalFusionSocket: 1);
		var templates = CreateItemTemplates();

		var plan = ItemSocketService.CreateAddManastonePlan(item, ManastoneItemId, useFusionSlots: true, templates);

		Assert.True(plan.Succeeded);
		Assert.Equal(1, plan.AddedStone?.Slot);
		Assert.Equal(2, plan.AddedCategory);
		Assert.NotNull(plan.ItemUpdate);
		Assert.Equal([0, 1], plan.ItemUpdate.FusionStones.Select(stone => stone.Slot).ToArray());
	}

	[Fact]
	public void AddManastone_ReturnsFailureWhenNoMatchingSlotsRemain()
	{
		var item = CreateSword(
			1001,
			manaStones:
			[
				new ItemStoneSocket(SpecialManastoneItemId, 0),
				new ItemStoneSocket(ManastoneItemId, 1),
				new ItemStoneSocket(ManastoneItemId, 2),
				new ItemStoneSocket(ManastoneItemId, 3),
			]);
		var templates = CreateItemTemplates();

		var plan = ItemSocketService.CreateAddManastonePlan(item, ManastoneItemId, useFusionSlots: false, templates);

		Assert.False(plan.Succeeded);
		Assert.Equal(ManastoneAddFailure.NoAvailableSlot, plan.Failure);
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
		IReadOnlyList<ItemStoneSocket>? fusionStones = null,
		int fusionedItemId = 0,
		int optionalFusionSocket = 0)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = SwordItemId,
			Count = 1,
			Location = 0,
			Slot = 10,
			FusionedItem = fusionedItemId,
			OptionalFusionSocket = optionalFusionSocket,
			ManaStones = manaStones ?? Array.Empty<ItemStoneSocket>(),
			FusionStones = fusionStones ?? Array.Empty<ItemStoneSocket>(),
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(KinahItemId, "Kinah", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(SwordItemId, "Practice Sword", 0, 1 << 10, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1, ManastoneSlots: 4, SpecialManastoneSlots: 1),
			new ItemTemplateSummary(PlainSwordItemId, "Plain Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1),
			new ItemTemplateSummary(FusedSwordItemId, "Fusion Sword", 0, 0, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1, ManastoneSlots: 2, SpecialManastoneSlots: 1),
			new ItemTemplateSummary(ManastoneItemId, "Manastone: HP +20", 0, 0, 1, "MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(FusionStoneItemId, "Fusion Manastone: HP +20", 0, 0, 1, "MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(SpecialManastoneItemId, "Ancient Manastone: HP +20", 0, 0, 1, "SPECIAL_MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(
				GodstoneItemId,
				"Godstone: Fire",
				0,
				0,
				1,
				"GODSTONE",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				0,
				GodstoneInfo: new ItemGodstoneInfo(1, 1, 50, 50, 0, 0)),
			new ItemTemplateSummary(InvalidGodstoneItemId, "Invalid Godstone", 0, 0, 1, "GODSTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
		]);
	}
}
