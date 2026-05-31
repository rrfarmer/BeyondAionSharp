using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class TamperingMutationService
{
	public static TamperingMutationResult SetTemperingLevel(
		InventoryItem item,
		ItemTemplateSummary itemTemplate,
		int temperingLevel,
		Func<int, int, int>? nextInclusiveRandom = null)
	{
		// Java parity: model/templates/item/actions/TamperingAction.setTemperingLevel.
		var oldTemperingLevel = item.Tempering;
		var randomPlumeBonus = item.RandomPlumeBonus;
		if (string.Equals(itemTemplate.ItemGroup, "PLUME", StringComparison.Ordinal))
		{
			if (temperingLevel > 4)
			{
				var nextRandom = nextInclusiveRandom ?? DefaultInclusiveRandom;
				for (var level = oldTemperingLevel; level < temperingLevel; level++)
					randomPlumeBonus += string.Equals(itemTemplate.TemperingName, "TSHIRT_PHYSICAL", StringComparison.Ordinal)
						? nextRandom(0, 3)
						: nextRandom(0, 12);
			}
			else
			{
				randomPlumeBonus = 0;
			}
		}

		var updatedItem = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
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
			Tempering = temperingLevel,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = randomPlumeBonus,
			PersistentState = item.PersistentState,
			PendingTuneResult = item.PendingTuneResult,
		};
		updatedItem.ManaStones = item.ManaStones;
		updatedItem.FusionStones = item.FusionStones;
		updatedItem.Godstone = item.Godstone;
		updatedItem.IdianStone = item.IdianStone;

		return new TamperingMutationResult(
			updatedItem,
			item.IsEquipped ? TamperingDirtyTarget.Equipment : TamperingDirtyTarget.InventoryStorage);
	}

	private static int DefaultInclusiveRandom(int minInclusive, int maxInclusive)
	{
		return Random.Shared.Next(minInclusive, maxInclusive + 1);
	}
}

public enum TamperingDirtyTarget
{
	InventoryStorage,
	Equipment,
}

public sealed record TamperingMutationResult(
	InventoryItem UpdatedItem,
	TamperingDirtyTarget DirtyTarget);
