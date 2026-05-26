using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum TeleportTransportationPricePlanStatus
{
	Ready,
	NotEnoughKinah,
}

public sealed record TeleportTransportationPricePlan(
	TeleportTransportationPricePlanStatus Status,
	long BasePrice,
	long TransportationPrice,
	bool UsedHiPass,
	InventoryItem? KinahItemUpdate,
	string JavaSource,
	bool IsLive);

public static class TeleportTransportationPricePlanService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;

	public static TeleportTransportationPricePlan CreatePlan(
		Player player,
		int locationBasePrice,
		bool hasHiPassEffect = false,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
	{
		// Java parity: services/teleport/TeleportService.checkKinahForTransportation.
		// This covers ordinary TeleportLocation pricing only; BindPointTeleportService distance pricing is separate.
		var transportationPrice = hasHiPassEffect
			? 1
			: PricesService.GetPriceForService(
				locationBasePrice,
				player.Race,
				priceOptions ?? new GameServerPriceOptions(),
				influenceRates ?? new PriceInfluenceRates());
		var kinahItem = player.InventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinahItem == null || kinahItem.Count < transportationPrice)
		{
			return new TeleportTransportationPricePlan(
				TeleportTransportationPricePlanStatus.NotEnoughKinah,
				locationBasePrice,
				transportationPrice,
				hasHiPassEffect,
				KinahItemUpdate: null,
				"TeleportService.checkKinahForTransportation -> STR_MSG_NOT_ENOUGH_KINA",
				IsLive: false);
		}

		return new TeleportTransportationPricePlan(
			TeleportTransportationPricePlanStatus.Ready,
			locationBasePrice,
			transportationPrice,
			hasHiPassEffect,
			CopyInventoryItem(kinahItem, kinahItem.Count - transportationPrice),
			"TeleportService.checkKinahForTransportation -> PricesService.getPriceForService -> Storage.tryDecreaseKinah(ItemUpdateType.DEC_KINAH_FLY)",
			IsLive: false);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
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
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
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
