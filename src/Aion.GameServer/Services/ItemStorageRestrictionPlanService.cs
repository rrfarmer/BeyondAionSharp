using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ItemStorageRestrictionPlanStatus
{
	Allowed,
	BlockedLegionWarehouseNoWithdrawalRight,
	BlockedRegularWarehouseItemRestricted,
	BlockedAccountWarehouseItemRestricted,
	BlockedLegionWarehouseItemRestricted,
	BlockedLegionWarehouseNoDepositRight,
}

public sealed record ItemStorageRestrictionPlan(
	ItemStorageRestrictionPlanStatus Status,
	string StorageType,
	SmSystemMessage? DenialMessage,
	bool IsRestricted,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ItemStorageRestrictionPlanService
{
	public static ItemStorageRestrictionPlan CreateIsItemRestrictedFromPlan(
		string storageType,
		bool isLegionWarehouseEnabled,
		bool isLegionMember,
		bool hasWHWithdrawalRight)
	{
		// Java parity: services/item/ItemRestrictionService.isItemRestrictedFrom.
		if (string.Equals(storageType, "LEGION_WAREHOUSE", StringComparison.OrdinalIgnoreCase))
		{
			if (!isLegionWarehouseEnabled || !isLegionMember || !hasWHWithdrawalRight)
			{
				return new ItemStorageRestrictionPlan(
					ItemStorageRestrictionPlanStatus.BlockedLegionWarehouseNoWithdrawalRight,
					storageType,
					SmSystemMessage.GuildWarehouseNoRight(),
					IsRestricted: true,
					"ItemRestrictionService.isItemRestrictedFrom -> LEGION_WAREHOUSE -> !enabled || !member || !withdrawal -> STR_GUILD_WAREHOUSE_NO_RIGHT"
				);
			}
		}

		return new ItemStorageRestrictionPlan(
			ItemStorageRestrictionPlanStatus.Allowed,
			storageType,
			DenialMessage: null,
			IsRestricted: false,
			$"ItemRestrictionService.isItemRestrictedFrom -> {storageType} -> allowed"
		);
	}

	public static ItemStorageRestrictionPlan CreateIsItemRestrictedToPlan(
		string storageType,
		bool itemIsStorableInWarehouse,
		bool itemIsStorableInAccWarehouse,
		bool itemIsStorableInLegWarehouse,
		bool isLegionWarehouseEnabled,
		bool isLegionMember,
		bool hasWHDepositRight,
		bool hasWHWithdrawalRight)
	{
		// Java parity: services/item/ItemRestrictionService.isItemRestrictedTo.
		var storageUpper = storageType.ToUpperInvariant();

		if (storageUpper == "REGULAR_WAREHOUSE")
		{
			if (!itemIsStorableInWarehouse)
			{
				return new ItemStorageRestrictionPlan(
					ItemStorageRestrictionPlanStatus.BlockedRegularWarehouseItemRestricted,
					storageType,
					SmSystemMessage.WarehouseCantDepositItem(),
					IsRestricted: true,
					"ItemRestrictionService.isItemRestrictedTo -> REGULAR_WAREHOUSE -> !isStorableInWarehouse -> STR_WAREHOUSE_CANT_DEPOSIT_ITEM"
				);
			}
		}
		else if (storageUpper == "ACCOUNT_WAREHOUSE")
		{
			if (!itemIsStorableInAccWarehouse)
			{
				return new ItemStorageRestrictionPlan(
					ItemStorageRestrictionPlanStatus.BlockedAccountWarehouseItemRestricted,
					storageType,
					SmSystemMessage.WarehouseCantAccountDeposit(),
					IsRestricted: true,
					"ItemRestrictionService.isItemRestrictedTo -> ACCOUNT_WAREHOUSE -> !isStorableInAccWarehouse -> STR_MSG_WAREHOUSE_CANT_ACCOUNT_DEPOSIT"
				);
			}
		}
		else if (storageUpper == "LEGION_WAREHOUSE")
		{
			if (!itemIsStorableInLegWarehouse || !isLegionWarehouseEnabled)
			{
				return new ItemStorageRestrictionPlan(
					ItemStorageRestrictionPlanStatus.BlockedLegionWarehouseItemRestricted,
					storageType,
					SmSystemMessage.WarehouseCantLegionDeposit(),
					IsRestricted: true,
					"ItemRestrictionService.isItemRestrictedTo -> LEGION_WAREHOUSE -> !storableInLeg || !enabled -> STR_MSG_WAREHOUSE_CANT_LEGION_DEPOSIT"
				);
			}

			if (!isLegionMember || (!hasWHDepositRight && !hasWHWithdrawalRight))
			{
				return new ItemStorageRestrictionPlan(
					ItemStorageRestrictionPlanStatus.BlockedLegionWarehouseNoDepositRight,
					storageType,
					SmSystemMessage.GuildWarehouseNoRight(),
					IsRestricted: true,
					"ItemRestrictionService.isItemRestrictedTo -> LEGION_WAREHOUSE -> !member || !deposit && !withdrawal -> STR_GUILD_WAREHOUSE_NO_RIGHT"
				);
			}
		}

		return new ItemStorageRestrictionPlan(
			ItemStorageRestrictionPlanStatus.Allowed,
			storageType,
			DenialMessage: null,
			IsRestricted: false,
			$"ItemRestrictionService.isItemRestrictedTo -> {storageType} -> allowed"
		);
	}
}
