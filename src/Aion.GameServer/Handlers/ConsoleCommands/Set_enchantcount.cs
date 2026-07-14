using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Set_enchantcount (ginho1).</summary>
public class Set_enchantcount : ConsoleCommand
{
    public Set_enchantcount()
        : base("set_enchantcount")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        int objId;
        int enchant;

        try
        {
            objId = ParseInt(paramsArr[0]);
            enchant = ParseInt(paramsArr[1]);
        }
        catch (FormatException)
        {
            PacketSendUtility.SendMessage(admin, "Invalid params.");
            return;
        }

        Item item = admin.GetInventory().GetItemByObjId(objId);
        if (item == null)
            item = admin.GetEquipment().GetEquippedItemByObjId(objId);

        if (item != null)
        {
            int curEnchant = item.GetEnchantLevel();
            int maxEnchant = item.GetItemTemplate().GetMaxEnchantLevel();
            maxEnchant += item.GetItemTemplate().GetMaxEnchantBonus();

            if (curEnchant + enchant <= maxEnchant)
            {
                curEnchant += enchant;
                item.SetEnchantLevel(Math.Min(curEnchant, maxEnchant));
                if (item.IsEquipped())
                    admin.GetGameStats().UpdateStatsVisually();

                ItemPacketService.UpdateItemAfterInfoChange(admin, item, ItemPacketService.ItemUpdateType.STATS_CHANGE);
                if (item.GetEnchantEffect() != null)
                {
                    item.GetEnchantEffect().EndEffect(admin);
                    item.SetEnchantEffect(null);
                }

                if (item.GetEnchantLevel() > 0 && item.IsEquipped())
                {
                    EnchantService.ApplyEnchantEffect(item, admin, item.GetEnchantLevel());
                }
                if (item.IsEquipped())
                    admin.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                else
                    admin.GetInventory().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);

                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEED_NEW(item.GetL10n(), enchant));
            }
            else
            {
                PacketSendUtility.SendMessage(admin, "Max Enchat for this item is: " + maxEnchant);
            }
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "Invalid item.");
            return;
        }
    }

    public void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///set_enchantcount <item Id> <enchantcount> <unk>");
    }
}
