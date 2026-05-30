using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreSellNotificationPlanStatus
{
	PlanCreated,
	BlockedEmptyItemName,
	BlockedZeroOrNegativeCount,
}

public sealed record PrivateStoreSellNotificationPlan(
	PrivateStoreSellNotificationPlanStatus Status,
	long Count,
	string? ItemName,
	SmSystemMessage? NotificationMessage,
	bool ShouldSendToSeller,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PrivateStoreSellNotificationPlanService
{
	public static PrivateStoreSellNotificationPlan CreatePlan(long count, string? itemName)
	{
		// Java parity: services/PrivateStoreService.sellStoreItem per-item notification to seller.
		// If count == 1: PacketSendUtility.sendPacket(seller, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM(item.getL10n()))
		// If count > 1: PacketSendUtility.sendPacket(seller, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM_MULTI(boughtItem.getCount(), item.getL10n()))
		if (string.IsNullOrEmpty(itemName))
		{
			return new PrivateStoreSellNotificationPlan(
				PrivateStoreSellNotificationPlanStatus.BlockedEmptyItemName,
				count,
				itemName,
				NotificationMessage: null,
				ShouldSendToSeller: false,
				"PrivateStoreService.sellStoreItem -> item.getL10n() is empty; notification not sent"
			);
		}

		if (count <= 0)
		{
			return new PrivateStoreSellNotificationPlan(
				PrivateStoreSellNotificationPlanStatus.BlockedZeroOrNegativeCount,
				count,
				itemName,
				NotificationMessage: null,
				ShouldSendToSeller: false,
				"PrivateStoreService.sellStoreItem -> count <= 0; notification not sent"
			);
		}

		var message = count == 1
			? SmSystemMessage.PersonalShopSellItem(itemName)
			: SmSystemMessage.PersonalShopSellItemMulti(count, itemName);

		return new PrivateStoreSellNotificationPlan(
			PrivateStoreSellNotificationPlanStatus.PlanCreated,
			count,
			itemName,
			message,
			ShouldSendToSeller: true,
			count == 1
				? $"PrivateStoreService.sellStoreItem -> count==1 -> STR_MSG_PERSONAL_SHOP_SELL_ITEM({itemName})"
				: $"PrivateStoreService.sellStoreItem -> count>1 -> STR_MSG_PERSONAL_SHOP_SELL_ITEM_MULTI({count}, {itemName})"
		);
	}
}
