using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum ArmsfusionFailure
{
	None,
	EquippedItem,
	ItemNoTarget,
	NotAvailable,
	NotEnoughKinah,
	TemporaryExchangeItem,
	DifferentType,
	MainRequireHigherLevel,
	NotComparableItem,
}

public sealed record ArmsfusionPricePlan(
	long BasePricePerLevelSquared,
	int MainWeaponLevel,
	long BasePrice,
	long FusionPrice,
	string JavaSource,
	bool IsLive);

public sealed record ArmsfusionFusionPlan(
	bool Succeeded,
	ArmsfusionFailure Failure,
	ArmsfusionPricePlan? PricePlan,
	InventoryItem? MainWeaponUpdate,
	InventoryItem? FuseWeaponUpdate,
	int? DeletedFuseWeaponObjectId,
	InventoryItem? KinahItemUpdate,
	long FusionPrice,
	string JavaSource,
	bool IsLive)
{
	public static ArmsfusionFusionPlan Failed(ArmsfusionFailure failure, ArmsfusionPricePlan? pricePlan = null)
	{
		return new ArmsfusionFusionPlan(
			Succeeded: false,
			failure,
			pricePlan,
			MainWeaponUpdate: null,
			FuseWeaponUpdate: null,
			DeletedFuseWeaponObjectId: null,
			KinahItemUpdate: null,
			pricePlan?.FusionPrice ?? 0,
			"ArmsfusionService.fusionWeapons",
			IsLive: false);
	}

	public static ArmsfusionFusionPlan Success(
		ArmsfusionPricePlan pricePlan,
		InventoryItem mainWeaponUpdate,
		InventoryItem? fuseWeaponUpdate,
		int? deletedFuseWeaponObjectId,
		InventoryItem kinahItemUpdate)
	{
		return new ArmsfusionFusionPlan(
			Succeeded: true,
			ArmsfusionFailure.None,
			pricePlan,
			mainWeaponUpdate,
			fuseWeaponUpdate,
			deletedFuseWeaponObjectId,
			kinahItemUpdate,
			pricePlan.FusionPrice,
			"ArmsfusionService.fusionWeapons validation/mutation plan",
			IsLive: false);
	}
}

public static class ArmsfusionPricePlanService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int CanCompositeWeaponMask = 1 << 11;

	public static ArmsfusionFusionPlan CreateFusionPlan(
		Player player,
		int mainWeaponObjectId,
		int fuseWeaponObjectId,
		ItemTemplateTable itemTemplates,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null,
		IReadOnlySet<int>? temporaryExchangeItemObjectIds = null)
	{
		// Java parity: services/ArmsfusionService.fusionWeapons. This is a non-live plan:
		// no InventoryDAO.store, ItemPacketService.updateItemAfterInfoChange, PacketSendUtility, or AuditLogger call.
		var inventoryItems = player.InventoryItems.ToList();
		var mainWeapon = FindBagItem(inventoryItems, mainWeaponObjectId);
		var fuseWeapon = FindBagItem(inventoryItems, fuseWeaponObjectId);
		if (mainWeapon == null || fuseWeapon == null)
		{
			return IsEquipped(inventoryItems, mainWeaponObjectId) || IsEquipped(inventoryItems, fuseWeaponObjectId)
				? ArmsfusionFusionPlan.Failed(ArmsfusionFailure.EquippedItem)
				: ArmsfusionFusionPlan.Failed(ArmsfusionFailure.ItemNoTarget);
		}

		var mainTemplate = itemTemplates.GetItemTemplate(mainWeapon.ItemId);
		var fuseTemplate = itemTemplates.GetItemTemplate(fuseWeapon.ItemId);
		if (mainTemplate == null || fuseTemplate == null || !IsCanFuse(mainTemplate) || !IsCanFuse(fuseTemplate))
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.NotAvailable);

		var pricePlan = CreatePlan(mainTemplate, player.Race, priceOptions, influenceRates);
		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < pricePlan.FusionPrice)
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.NotEnoughKinah, pricePlan);

		if (temporaryExchangeItemObjectIds?.Contains(mainWeaponObjectId) == true)
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.TemporaryExchangeItem, pricePlan);

		if (mainWeapon.FusionedItem != 0 || fuseWeapon.FusionedItem != 0)
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.NotAvailable, pricePlan);

		if (!string.Equals(mainTemplate.ItemGroup, fuseTemplate.ItemGroup, StringComparison.Ordinal))
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.DifferentType, pricePlan);

		if (fuseTemplate.Level > mainTemplate.Level)
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.MainRequireHigherLevel, pricePlan);

		if (mainTemplate.Improvement != null
			&& fuseTemplate.Improvement != null
			&& mainTemplate.Improvement.ChargeWay != fuseTemplate.Improvement.ChargeWay)
		{
			return ArmsfusionFusionPlan.Failed(ArmsfusionFailure.NotComparableItem, pricePlan);
		}

		var mainWeaponUpdate = CopyInventoryItem(
			mainWeapon,
			fusionedItem: fuseWeapon.ItemId,
			optionalFusionSocket: fuseWeapon.OptionalSocket,
			fusionRandomBonus: fuseWeapon.RandomBonus,
			charge: 0,
			tuneCount: GetFusionTuneCount(mainWeapon, mainTemplate));
		mainWeaponUpdate.FusionStones = fuseWeapon.ManaStones
			.Select(stone => new ItemStoneSocket(stone.ItemId, stone.Slot))
			.OrderBy(stone => stone.Slot)
			.ToArray();

		var fuseWeaponUpdate = fuseWeapon.Count > 1
			? CopyInventoryItem(fuseWeapon, count: fuseWeapon.Count - 1)
			: null;
		var deletedFuseWeaponObjectId = fuseWeapon.Count <= 1 ? fuseWeapon.ObjectId : (int?)null;
		var kinahItemUpdate = CopyInventoryItem(kinahItem, count: kinahItem.Count - pricePlan.FusionPrice);
		return ArmsfusionFusionPlan.Success(pricePlan, mainWeaponUpdate, fuseWeaponUpdate, deletedFuseWeaponObjectId, kinahItemUpdate);
	}

	public static ArmsfusionPricePlan CreatePlan(
		ItemTemplateSummary mainWeaponTemplate,
		string playerRace,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
	{
		// Java parity: services/ArmsfusionService.fusionWeapons calculates:
		// getBasePricePerLevelSquared(mainWeapon.quality) * level * level,
		// then services/trade/PricesService.getPriceForService(..., player.getRace()).
		var basePricePerLevelSquared = GetBasePricePerLevelSquared(mainWeaponTemplate.Quality);
		var level = mainWeaponTemplate.Level;
		var basePrice = basePricePerLevelSquared * level * level;
		var fusionPrice = PricesService.GetPriceForService(
			basePrice,
			playerRace,
			priceOptions ?? new GameServerPriceOptions(),
			influenceRates ?? new PriceInfluenceRates());
		return new ArmsfusionPricePlan(
			basePricePerLevelSquared,
			level,
			basePrice,
			fusionPrice,
			"ArmsfusionService.fusionWeapons -> getBasePricePerLevelSquared -> PricesService.getPriceForService",
			IsLive: false);
	}

	public static long GetBasePricePerLevelSquared(string itemQuality)
	{
		// Java parity: services/ArmsfusionService.getBasePricePerLevelSquared.
		return itemQuality.ToUpperInvariant() switch
		{
			"JUNK" or "COMMON" => 200,
			"RARE" => 250,
			"LEGEND" => 300,
			"UNIQUE" => 400,
			"EPIC" => 500,
			_ => 600,
		};
	}

	private static InventoryItem? FindBagItem(IEnumerable<InventoryItem> inventoryItems, int objectId)
	{
		return inventoryItems.FirstOrDefault(item => item.ObjectId == objectId && item.Location == CubeStorageId && !item.IsEquipped);
	}

	private static bool IsEquipped(IEnumerable<InventoryItem> inventoryItems, int objectId)
	{
		return inventoryItems.Any(item => item.ObjectId == objectId && item.IsEquipped);
	}

	private static bool IsCanFuse(ItemTemplateSummary template)
	{
		// Java parity: model/templates/item/ItemTemplate.isCanFuse -> ItemMask.CAN_COMPOSITE_WEAPON.
		return (template.Mask & CanCompositeWeaponMask) == CanCompositeWeaponMask;
	}

	private static int GetFusionTuneCount(InventoryItem mainWeapon, ItemTemplateSummary mainTemplate)
	{
		// Java parity: Item.setFusionedItem calls updateChargeInfo(0), then removeRemainingTuningCountIfPossible.
		return mainWeapon.IsIdentified && mainTemplate.MaxTuneCount > 0 && mainWeapon.TuneCount != mainTemplate.MaxTuneCount
			? mainTemplate.MaxTuneCount
			: mainWeapon.TuneCount;
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		long? count = null,
		int? fusionedItem = null,
		int? optionalFusionSocket = null,
		int? fusionRandomBonus = null,
		int? charge = null,
		int? tuneCount = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count ?? item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = fusionedItem ?? item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = optionalFusionSocket ?? item.OptionalFusionSocket,
			Charge = charge ?? item.Charge,
			TuneCount = tuneCount ?? item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = fusionRandomBonus ?? item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}
