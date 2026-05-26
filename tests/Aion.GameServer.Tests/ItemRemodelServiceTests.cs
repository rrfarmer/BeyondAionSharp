using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemRemodelServiceTests
{
	private const int RemodelableMask = 1 << 12;
	private const int DyeableMask = 1 << 15;
	private const int KinahItemId = 182400001;

	[Fact]
	public void CreateRemodelPlan_AppliesExtractedSkinAndConsumesPaymentAndExtractItem()
	{
		var keepTemplate = CreateTemplate(100, "Practice Sword", "SWORD", validSlots: 3, mask: RemodelableMask);
		var extractTemplate = CreateTemplate(101, "Skin Sword", "SWORD", validSlots: 3, mask: RemodelableMask);
		var player = new Player { Race = "ELYOS" };
		var keepItem = new InventoryItem { ObjectId = 1, ItemId = 100, Count = 1, Location = 0 };
		var extractItem = new InventoryItem { ObjectId = 2, ItemId = 101, Count = 2, Location = 0, Color = 0x112233 };
		var kinahItem = new InventoryItem { ObjectId = 3, ItemId = KinahItemId, Count = 2000, Location = 0 };

		var plan = ItemRemodelService.CreateRemodelPlan(player, keepItem, keepTemplate, extractItem, extractTemplate, extractTemplate, kinahItem, playerLevel: 10);

		Assert.True(plan.Succeeded);
		Assert.Equal(101, plan.TargetItemUpdate!.ItemSkin);
		Assert.Equal(0x112233, plan.TargetItemUpdate.Color);
		Assert.Equal(1000, plan.RemodelPrice);
		Assert.Equal(1000, plan.KinahItemUpdate!.Count);
		Assert.Equal(1, plan.ExtractItemUpdate!.Count);
		Assert.Null(plan.DeletedExtractItemObjectId);
	}

	[Fact]
	public void CreateRemodelPlan_UsesJavaPricesServiceForKinahFee()
	{
		var keepTemplate = CreateTemplate(100, "Practice Sword", "SWORD", validSlots: 3, mask: RemodelableMask);
		var extractTemplate = CreateTemplate(101, "Skin Sword", "SWORD", validSlots: 3, mask: RemodelableMask);
		var player = new Player { Race = "ELYOS" };
		var keepItem = new InventoryItem { ObjectId = 1, ItemId = 100, Count = 1, Location = 0 };
		var extractItem = new InventoryItem { ObjectId = 2, ItemId = 101, Count = 1, Location = 0, Color = 0x445566 };
		var kinahItem = new InventoryItem { ObjectId = 3, ItemId = KinahItemId, Count = 2_000, Location = 0 };
		var priceOptions = new GameServerPriceOptions
		{
			DefaultPrices = 110,
			DefaultModifier = 90,
			DefaultTaxes = 105,
		};

		var plan = ItemRemodelService.CreateRemodelPlan(
			player,
			keepItem,
			keepTemplate,
			extractItem,
			extractTemplate,
			extractTemplate,
			kinahItem,
			playerLevel: 10,
			priceOptions,
			new PriceInfluenceRates(Elyos: 0.3f, Asmodians: 0.5f));

		Assert.True(plan.Succeeded);
		Assert.Equal(1188, plan.RemodelPrice);
		Assert.Equal(812, plan.KinahItemUpdate!.Count);
	}

	[Fact]
	public void CreateRemodelPlan_PatternReshaperRemovesSkinAndDeletesSingleExtractor()
	{
		var keepTemplate = CreateTemplate(100, "Practice Sword", "SWORD", validSlots: 3, mask: RemodelableMask);
		var patternTemplate = CreateTemplate(ItemRemodelService.PatternReshaperItemId, "Pattern Reshaper", "NONE", validSlots: 0, mask: RemodelableMask);
		var keepItem = new InventoryItem { ObjectId = 1, ItemId = 100, Count = 1, Location = 0, ItemSkin = 101, Color = 0x112233 };
		var extractItem = new InventoryItem { ObjectId = 2, ItemId = patternTemplate.TemplateId, Count = 1, Location = 0 };
		var kinahItem = new InventoryItem { ObjectId = 3, ItemId = KinahItemId, Count = 2000, Location = 0 };

		var plan = ItemRemodelService.CreateRemodelPlan(new Player { Race = "ELYOS" }, keepItem, keepTemplate, extractItem, patternTemplate, patternTemplate, kinahItem, playerLevel: 10);

		Assert.True(plan.Succeeded);
		Assert.Equal(0, plan.TargetItemUpdate!.ItemSkin);
		Assert.Null(plan.TargetItemUpdate.Color);
		Assert.Equal(1000, plan.KinahItemUpdate!.Count);
		Assert.Null(plan.ExtractItemUpdate);
		Assert.Equal(2, plan.DeletedExtractItemObjectId);
	}

	[Fact]
	public void CreateRemodelPlan_RejectsLowLevelPlayers()
	{
		var template = CreateTemplate(100, "Practice Sword", "SWORD", validSlots: 3, mask: RemodelableMask | DyeableMask);
		var item = new InventoryItem { ObjectId = 1, ItemId = 100, Count = 1, Location = 0 };
		var kinahItem = new InventoryItem { ObjectId = 3, ItemId = KinahItemId, Count = 2000, Location = 0 };

		var plan = ItemRemodelService.CreateRemodelPlan(new Player(), item, template, item, template, template, kinahItem, playerLevel: 9);

		Assert.False(plan.Succeeded);
		Assert.Equal(ItemRemodelFailure.LevelLimit, plan.Failure);
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, string name, string itemGroup, long validSlots, int mask)
	{
		return new ItemTemplateSummary(
			itemId,
			name,
			DescriptionId: 1,
			Mask: mask,
			Level: 1,
			ItemGroup: itemGroup,
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: validSlots);
	}
}
