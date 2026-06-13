using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemRemodelService (Sarynth, Wakizashi). remodelItem: applies an extract item's skin to a keep item (level/gender/kinah checks, Pattern Reshaper 168100000 to revert skin, item-group/subtype compatibility, remodelable checks, dye transfer). getItemSubType().equals/== preserved; ItemPacketService.updateItemAfterInfoChange. Gender/ItemGroup/ItemSubType/ItemActions/SM_ red-tolerated.</summary>
public class ItemRemodelService
{
    public static void RemodelItem(Player player, int keepItemObjId, int extractItemObjId)
    {
        Storage inventory = player.GetInventory();
        Item keepItem = inventory.GetItemByObjId(keepItemObjId);
        Item extractItem = inventory.GetItemByObjId(extractItemObjId);

        long remodelCost = Aion.GameServer.Services.Trade.PricesService.GetPriceForService(1000, player.GetRace());

        if (keepItem == null || extractItem == null) // NPE check.
            return;

        // Check Player Level
        if (player.GetLevel() < 10)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_PC_LEVEL_LIMIT());
            return;
        }

        if (keepItem.GetItemTemplate().GetUseLimits() != null && extractItem.GetItemTemplate().GetUseLimits() != null)
        {
            Gender keepItemGender = keepItem.GetItemTemplate().GetUseLimits().GetGenderPermitted();
            Gender extractItemGender = extractItem.GetItemTemplate().GetUseLimits().GetGenderPermitted();
            if (keepItemGender != null && extractItemGender != null)
            {
                if (keepItemGender != extractItemGender)
                {
                    string item1 = keepItem.GetItemTemplate().GetL10n();
                    string item2 = extractItem.GetItemTemplate().GetL10n();
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_CHANGE_SKIN_OPPOSITE_REQUIREMENT(item1, item2));
                    return;
                }
            }
        }

        // Check Kinah
        if (player.GetInventory().GetKinah() < remodelCost)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NOT_ENOUGH_GOLD(keepItem.GetItemTemplate().GetL10n()));
            return;
        }

        // Check for using "Pattern Reshaper" (168100000)
        if (extractItem.GetItemTemplate().GetTemplateId() == 168100000)
        {
            if (!keepItem.IsSkinnedItem())
            {
                PacketSendUtility.SendMessage(player, "That item does not have a remodeled skin to remove.");
                return;
            }
            // Remove Money
            if (!player.GetInventory().TryDecreaseKinah(remodelCost))
                return;
            // Remove Pattern Reshaper
            player.GetInventory().DecreaseItemCount(extractItem, 1);

            // Revert item to ORIGINAL SKIN
            keepItem.SetItemSkinTemplate(keepItem.GetItemTemplate());

            // Remove dye color if item can not be dyed.
            if (!keepItem.GetItemTemplate().IsItemDyePermitted())
                keepItem.SetItemColor(0);

            // Notify Player
            ItemPacketService.UpdateItemAfterInfoChange(player, keepItem);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_SUCCEED(keepItem.GetItemTemplate().GetL10n()));
            return;
        }
        // Check that types match.
        ItemGroup keep = keepItem.GetItemTemplate().GetItemGroup();
        ItemGroup extract = extractItem.GetItemSkinTemplate().GetItemGroup();
        if ((keep != extract && !(extract.GetItemSubType().Equals(ItemSubType.CLOTHES)
            || extract.GetItemSubType() == ItemSubType.ALL_ARMOR && keep.GetValidEquipmentSlots() == extract.GetValidEquipmentSlots()))
            || keep.GetItemSubType().Equals(ItemSubType.CLOTHES))
        {
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NOT_COMPATIBLE(keepItem.GetItemTemplate().GetL10n(), extractItem.GetItemSkinTemplate().GetL10n()));
            return;
        }

        if (!keepItem.IsRemodelable())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NOT_SKIN_CHANGABLE_ITEM(keepItem.GetItemTemplate().GetL10n()));
            return;
        }

        if (!extractItem.IsRemodelable())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_CAN_NOT_REMOVE_SKIN_ITEM(extractItem.GetItemTemplate().GetL10n()));
            return;
        }

        ItemTemplate skin = extractItem.GetItemSkinTemplate();
        ItemActions actions = skin.GetActions();
        if (extractItem.IsSkinnedItem() && actions != null && actions.GetRemodelAction() != null && actions.GetRemodelAction().GetExtractType() == 2)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_CAN_NOT_REMOVE_SKIN_ITEM(extractItem.GetItemTemplate().GetL10n()));
            return;
        }
        // -- SUCCESS --

        // Remove Money
        player.GetInventory().DecreaseKinah(remodelCost);

        // Remove Item
        player.GetInventory().DecreaseItemCount(extractItem, 1);

        // REMODEL ITEM
        keepItem.SetItemSkinTemplate(skin);

        // Transfer Dye
        keepItem.SetItemColor(extractItem.GetItemColor());

        // Notify Player
        ItemPacketService.UpdateItemAfterInfoChange(player, keepItem);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_SUCCEED(keepItem.GetItemTemplate().GetL10n()));
    }
}
