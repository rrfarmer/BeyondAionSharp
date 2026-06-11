using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemRestrictionService (ATracer). Storage move restrictions: isItemRestrictedFrom (legion-warehouse withdrawal rights), isItemRestrictedTo (warehouse/account/legion deposit storability + legion rights), canRemoveItem (quest-item placeholder). switch-on-enum->switch statement; getItemGroup().equals->Equals. StorageType/LegionPermissionsMask/ItemGroup/SM_ red-tolerated.</summary>
public class ItemRestrictionService
{
    /// <summary>
    /// Check if item can be moved from storage by player
    /// </summary>
    public static bool IsItemRestrictedFrom(Player player, Item item, StorageType storageType)
    {
        switch (storageType)
        {
            case StorageType.LEGION_WAREHOUSE:
                if (!LegionConfig.LEGION_WAREHOUSE || !player.IsLegionMember() || !player.GetLegionMember().HasRights(LegionPermissionsMask.WH_WITHDRAWAL))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                    return true;
                }
                break;
        }
        return false;
    }

    /// <summary>
    /// Check if item can be moved to storage by player
    /// </summary>
    public static bool IsItemRestrictedTo(Player player, Item item, StorageType storageType)
    {
        switch (storageType)
        {
            case StorageType.REGULAR_WAREHOUSE:
                if (!item.IsStorableInWarehouse())
                {
                    // You cannot store this in the warehouse.
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WAREHOUSE_CANT_DEPOSIT_ITEM());
                    return true;
                }
                break;
            case StorageType.ACCOUNT_WAREHOUSE:
                if (!item.IsStorableInAccWarehouse())
                {
                    // You cannot store this item in the account warehouse.
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_WAREHOUSE_CANT_ACCOUNT_DEPOSIT());
                    return true;
                }
                break;
            case StorageType.LEGION_WAREHOUSE:
                if (!item.IsStorableInLegWarehouse() || !LegionConfig.LEGION_WAREHOUSE)
                {
                    // You cannot store this item in the Legion warehouse.
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_WAREHOUSE_CANT_LEGION_DEPOSIT());
                    return true;
                }
                else if (!player.IsLegionMember() || (!player.GetLegionMember().HasRights(LegionPermissionsMask.WH_DEPOSIT)
                    && !player.GetLegionMember().HasRights(LegionPermissionsMask.WH_WITHDRAWAL)))
                {
                    // You do not have the authority to use the Legion warehouse.
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>Check whether the item can be removed</summary>
    public static bool CanRemoveItem(Player player, Item item)
    {
        ItemTemplate it = item.GetItemTemplate();
        if (it.GetItemGroup().Equals(ItemGroup.QUEST))
        {
            // TODO: not removable, if quest status start and quest can not be abandoned
            // Waiting for quest data reparse
            return true;
        }
        return true;
    }
}
