using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreItemValidationPlanStatus
{
	Valid,
	BlockedNullOrMismatchedItem,
	BlockedInvalidCount,
	BlockedNegativePrice,
	BlockedStoreIsFull,
	BlockedNotTradeable,
	BlockedItemIsEquipped,
	BlockedAlreadyRegistered,
}

public sealed record PrivateStoreItemValidationPlan(
	PrivateStoreItemValidationPlanStatus Status,
	SmSystemMessage? DenialMessage,
	bool IsValid,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PrivateStoreItemValidationPlanService
{
	public const int MaxStoreItemSlots = 10;

	public static PrivateStoreItemValidationPlan CreatePlan(
		bool itemExistsAndIdMatches,
		long requestedCount,
		long availableCount,
		long requestedPrice,
		int currentStoreItemCount,
		bool itemIsPackCountAboveZeroOrTradeable,
		bool itemIsEquipped,
		bool itemAlreadyRegistered)
	{
		// Java parity: services/PrivateStoreService.validateItem guard order.
		if (!itemExistsAndIdMatches)
			return SilentBlock(PrivateStoreItemValidationPlanStatus.BlockedNullOrMismatchedItem,
				"PrivateStoreService.validateItem -> item == null || psItem.getItemId() != itemTemplate.getTemplateId() -> return false");

		if (requestedCount < 1 || requestedCount > availableCount)
			return SilentBlock(PrivateStoreItemValidationPlanStatus.BlockedInvalidCount,
				"PrivateStoreService.validateItem -> psItem.getCount() > item.getItemCount() || psItem.getCount() < 1 -> return false");

		if (requestedPrice < 0)
			return SilentBlock(PrivateStoreItemValidationPlanStatus.BlockedNegativePrice,
				"PrivateStoreService.validateItem -> psItem.getPrice() < 0 -> return false");

		if (currentStoreItemCount >= MaxStoreItemSlots)
			return Block(PrivateStoreItemValidationPlanStatus.BlockedStoreIsFull,
				SmSystemMessage.PersonalShopFullBasket(),
				"PrivateStoreService.validateItem -> store.getSoldItems().size() == 10 -> STR_PERSONAL_SHOP_FULL_BASKET");

		if (!itemIsPackCountAboveZeroOrTradeable)
			return Block(PrivateStoreItemValidationPlanStatus.BlockedNotTradeable,
				SmSystemMessage.PersonalShopCannotBeExchanged(),
				"PrivateStoreService.validateItem -> item.getPackCount() <= 0 && !item.isTradeable() -> STR_PERSONAL_SHOP_CANNOT_BE_EXCHANGED");

		if (itemIsEquipped)
			return Block(PrivateStoreItemValidationPlanStatus.BlockedItemIsEquipped,
				SmSystemMessage.PersonalShopCanNotSellEquippedItem(),
				"PrivateStoreService.validateItem -> item.isEquipped() -> STR_PERSONAL_SHOP_CAN_NOT_SELL_EQUIPED_ITEM");

		if (itemAlreadyRegistered)
			return Block(PrivateStoreItemValidationPlanStatus.BlockedAlreadyRegistered,
				SmSystemMessage.PersonalShopAlreadyRegistItem(),
				"PrivateStoreService.validateItem -> store.getTradeItemByObjId(psItem.getItemObjId()) != null -> STR_PERSONAL_SHOP_ALREAY_REGIST_ITEM");

		return new PrivateStoreItemValidationPlan(
			PrivateStoreItemValidationPlanStatus.Valid,
			DenialMessage: null,
			IsValid: true,
			"PrivateStoreService.validateItem -> all guards passed -> return true");
	}

	private static PrivateStoreItemValidationPlan Block(PrivateStoreItemValidationPlanStatus status, SmSystemMessage message, string source)
	{
		return new PrivateStoreItemValidationPlan(status, message, IsValid: false, source);
	}

	private static PrivateStoreItemValidationPlan SilentBlock(PrivateStoreItemValidationPlanStatus status, string source)
	{
		return new PrivateStoreItemValidationPlan(status, DenialMessage: null, IsValid: false, source);
	}
}
