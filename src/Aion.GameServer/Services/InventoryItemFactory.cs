using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InventoryItemFactory
{
	public const int KinahItemId = 182400001;

	public static InventoryItem CreateNewItem(
		int objectId,
		ItemTemplateSummary itemTemplate,
		long count,
		int ownerId,
		int location,
		long slot)
	{
		// Java parity: services/item/ItemFactory.newItem(itemId, count) default item state and count clamp.
		var expireTime = itemTemplate.ExpireTimeMinutes == 0
			? 0
			: (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + itemTemplate.ExpireTimeMinutes * 60 - 1;

		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemTemplate.TemplateId,
			Count = CalculateNewItemCount(itemTemplate, count),
			OwnerId = ownerId,
			Location = location,
			Slot = slot,
			ExpireTime = expireTime,
			ActivationCount = itemTemplate.ActivationCount,
			TuneCount = itemTemplate.CanTune ? -1 : 0,
			IsAmplified = itemTemplate.EnchantType == 1,
		};
	}

	public static long CalculateNewItemCount(ItemTemplateSummary itemTemplate, long count)
	{
		// Java parity: ItemFactory.calculateCount exempts kinah from max-stack clamping.
		return count > itemTemplate.MaxStackCount && itemTemplate.TemplateId != KinahItemId
			? itemTemplate.MaxStackCount
			: count;
	}
}
