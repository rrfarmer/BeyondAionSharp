using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationApServiceTests
{
	[Fact]
	public void CreatePurificationApPlan_SpendsAbyssPointsOnlyAfterMaterialsDecrease()
	{
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			materials: [new InventoryItem { ObjectId = 3, ItemId = 186000001, Count = 2, Location = 0 }]);
		var baseItem = CreateBaseItem(enchant: 15);
		var result = CreateResult(necessaryAbyssPoints: 1_200, requiredMaterials: [new(186000001, 2)]);

		var pending = ItemPurificationApService.CreatePurificationApPlan(
			player,
			baseItem,
			result,
			materialsAlreadyDecreased: false);
		var applied = ItemPurificationApService.CreatePurificationApPlan(
			player,
			baseItem,
			result,
			materialsAlreadyDecreased: true);

		Assert.Equal(ItemPurificationApStatus.AllowedPendingMaterialDecrease, pending.Status);
		Assert.Null(pending.AbyssPointsPlan);
		Assert.Equal(ItemPurificationApStatus.Applied, applied.Status);
		Assert.NotNull(applied.AbyssPointsPlan);
		Assert.Equal(-1_200, applied.AbyssPointsPlan.Added);
		Assert.Equal(3_800, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreatePurificationApPlan_ResolvesResultFromStaticDataTable()
	{
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			materials: [new InventoryItem { ObjectId = 3, ItemId = 186000001, Count = 2, Location = 0 }]);
		var baseItem = CreateBaseItem(enchant: 15);
		var table = CreateTable();

		var plan = ItemPurificationApService.CreatePurificationApPlan(
			player,
			baseItem,
			table,
			resultItemId: 100000002,
			materialsAlreadyDecreased: true);

		Assert.Equal(ItemPurificationApStatus.Applied, plan.Status);
		Assert.Equal(1_200, plan.NecessaryAbyssPoints);
		Assert.NotNull(plan.AbyssPointsPlan);
		Assert.Equal(3_800, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreatePurificationApPlan_ReportsMissingTemplateAndInvalidResult()
	{
		var player = CreatePlayer(abyssPoints: 5_000, kinah: 10_000);
		var table = CreateTable();

		var missingTemplate = ItemPurificationApService.CreatePurificationApPlan(
			player,
			CreateBaseItem(itemId: 999, enchant: 15),
			table,
			resultItemId: 100000002,
			materialsAlreadyDecreased: true);
		var invalidResult = ItemPurificationApService.CreatePurificationApPlan(
			player,
			CreateBaseItem(enchant: 15),
			table,
			resultItemId: 999,
			materialsAlreadyDecreased: true);

		Assert.Equal(ItemPurificationApStatus.MissingTemplate, missingTemplate.Status);
		Assert.Equal(ItemPurificationApStatus.InvalidResultItem, invalidResult.Status);
	}

	[Fact]
	public void ValidatePurificationAllowed_RejectsInsufficientAbyssPointsBeforeSpend()
	{
		var player = CreatePlayer(abyssPoints: 1_199, kinah: 10_000);
		var baseItem = CreateBaseItem(enchant: 15);
		var result = CreateResult(necessaryAbyssPoints: 1_200);

		var plan = ItemPurificationApService.CreatePurificationApPlan(
			player,
			baseItem,
			result,
			materialsAlreadyDecreased: true);

		Assert.Equal(ItemPurificationApStatus.NotEnoughAbyssPoints, plan.Status);
		Assert.Null(plan.AbyssPointsPlan);
		Assert.Equal(1_199, player.AbyssRank.Ap);
	}

	[Fact]
	public void ValidatePurificationAllowed_ChecksIdentifiedEnchantKinahAndMaterials()
	{
		var result = CreateResult(
			minEnchantCount: 10,
			necessaryAbyssPoints: 500,
			necessaryKinah: 2_000,
			requiredMaterials: [new(186000001, 3)]);

		var unidentified = ItemPurificationApService.ValidatePurificationAllowed(
			CreatePlayer(abyssPoints: 1_000, kinah: 5_000, materials: [new InventoryItem { ObjectId = 3, ItemId = 186000001, Count = 3, Location = 0 }]),
			CreateBaseItem(enchant: 10, tuneCount: -1),
			result);
		var lowEnchant = ItemPurificationApService.ValidatePurificationAllowed(
			CreatePlayer(abyssPoints: 1_000, kinah: 5_000, materials: [new InventoryItem { ObjectId = 3, ItemId = 186000001, Count = 3, Location = 0 }]),
			CreateBaseItem(enchant: 9),
			result);
		var noKinah = ItemPurificationApService.ValidatePurificationAllowed(
			CreatePlayer(abyssPoints: 1_000, kinah: 1_999, materials: [new InventoryItem { ObjectId = 3, ItemId = 186000001, Count = 3, Location = 0 }]),
			CreateBaseItem(enchant: 10),
			result);
		var missingMaterial = ItemPurificationApService.ValidatePurificationAllowed(
			CreatePlayer(abyssPoints: 1_000, kinah: 5_000, materials: [new InventoryItem { ObjectId = 3, ItemId = 186000001, Count = 2, Location = 0 }]),
			CreateBaseItem(enchant: 10),
			result);

		Assert.Equal(ItemPurificationApStatus.NotIdentified, unidentified.Status);
		Assert.Equal(ItemPurificationApStatus.EnchantTooLow, lowEnchant.Status);
		Assert.Equal(ItemPurificationApStatus.NotEnoughKinah, noKinah.Status);
		Assert.Equal(ItemPurificationApStatus.MissingRequiredMaterial, missingMaterial.Status);
		Assert.Equal(new ItemPurificationMaterialRequirement(186000001, 3), Assert.Single(missingMaterial.MissingMaterials));
	}

	[Fact]
	public void CreatePurificationApPlan_SkipsAbyssPointSpendWhenJavaCostIsZero()
	{
		var player = CreatePlayer(abyssPoints: 5_000, kinah: 10_000);
		var baseItem = CreateBaseItem(enchant: 15);
		var result = CreateResult(necessaryAbyssPoints: 0);

		var plan = ItemPurificationApService.CreatePurificationApPlan(
			player,
			baseItem,
			result,
			materialsAlreadyDecreased: true);

		Assert.Equal(ItemPurificationApStatus.AppliedNoAbyssPointsRequired, plan.Status);
		Assert.Null(plan.AbyssPointsPlan);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	private static Player CreatePlayer(
		int abyssPoints,
		long kinah,
		IReadOnlyList<InventoryItem>? materials = null)
	{
		return new Player
		{
			ObjectId = 1,
			Race = "ELYOS",
			AbyssRank = PlayerAbyssRank.Default() with { Ap = abyssPoints },
			InventoryItems =
			[
				new InventoryItem { ObjectId = 2, ItemId = 182400001, Count = kinah, Location = 0 },
				.. materials ?? Array.Empty<InventoryItem>(),
			],
		};
	}

	private static InventoryItem CreateBaseItem(int enchant, int tuneCount = 0, int itemId = 100000001)
	{
		return new InventoryItem
		{
			ObjectId = 10,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			Enchant = enchant,
			TuneCount = tuneCount,
		};
	}

	private static ItemPurificationResultProjection CreateResult(
		int necessaryAbyssPoints,
		int minEnchantCount = 10,
		long necessaryKinah = 0,
		IReadOnlyList<ItemPurificationMaterialRequirement>? requiredMaterials = null)
	{
		return new ItemPurificationResultProjection(
			ResultItemId: 100000002,
			MinEnchantCount: minEnchantCount,
			NecessaryAbyssPoints: necessaryAbyssPoints,
			NecessaryKinah: necessaryKinah,
			RequiredMaterials: requiredMaterials ?? Array.Empty<ItemPurificationMaterialRequirement>());
	}

	private static ItemPurificationTable CreateTable()
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
						NecessaryKinah: 0,
						RequiredMaterials: [new ItemPurificationMaterialSummary(186000001, 2)]),
				]),
		]);
	}
}
