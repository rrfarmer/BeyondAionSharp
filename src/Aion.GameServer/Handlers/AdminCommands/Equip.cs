using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Listeners;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Equip (Tago, Wakizashi).</summary>
public class Equip : AdminCommand
{
    public Equip()
        : base("equip", "Enchants all equipped items.")
    {
        SetSyntaxInfo(
            "socket <manastone link|ID> [limit] [player] - Sockets the manastone in all equipped items of your target or the given player.",
            "unsocket [player] - Removes manastones from all equipped items of your target or the given player.",
            "enchant <0-255> [player] - Enchant all equipped items of your target or the given player.",
            "temper <0-255> [player] - Temper all equipped items of your target or the given player.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[paramsArr.Length - 1]));
        if (player == null)
            player = admin.GetTarget() is Player target ? target : admin;
        if ("socket".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 2)
        {
            ItemTemplate manastone = DataManager.ITEM_DATA.GetItemTemplate(ChatUtil.GetItemId(paramsArr[1]));
            int count = paramsArr.Length < 3 ? int.MaxValue : int.Parse(paramsArr[2]);
            Socket(admin, player, manastone, count);
        }
        else if ("unsocket".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            Unsocket(admin, player);
        }
        else if ("enchant".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 2)
        {
            int enchant = Math.Max(0, Math.Min(255, int.Parse(paramsArr[1])));
            Enchant(admin, player, enchant);
        }
        else if ("temper".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 2)
        {
            int temperingLevel = Math.Max(0, Math.Min(255, int.Parse(paramsArr[1])));
            Temper(admin, player, temperingLevel);
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void Socket(Player admin, Player player, ItemTemplate manastone, int count)
    {
        if (count <= 0)
        {
            SendInfo(admin, "Count must be greater than 0.");
            return;
        }
        if (manastone == null || manastone.GetItemGroup() != ItemGroup.MANASTONE && manastone.GetItemGroup() != ItemGroup.SPECIAL_MANASTONE)
        {
            SendInfo(admin, "Invalid manastone.");
            return;
        }
        int manastoneId = manastone.GetTemplateId();
        int maxSocketed = 0;
        foreach (Item targetItem in player.GetEquipment().GetEquippedItemsWithoutStigma())
        {
            if (targetItem.GetSockets(false) == 0)
                continue;
            for (int counter = 0; counter < count;)
            {
                ManaStone manaStone = ItemSocketService.AddManaStone(targetItem, manastoneId, false);
                if (manaStone == null)
                    break;
                maxSocketed = Math.Max(maxSocketed, ++counter);
                ItemEquipmentListener.AddStoneStats(targetItem, manaStone, player.GetGameStats());
            }
            if (maxSocketed > 0)
            {
                ItemPacketService.UpdateItemAfterInfoChange(player, targetItem);
                targetItem.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            }
        }
        player.GetGameStats().UpdateStatsVisually();
        if (maxSocketed == 0)
            SendInfo(admin, "There are no free slots on any equipped items.");
        else if (player == admin)
            SendInfo(player, maxSocketed + "x " + ChatUtil.Item(manastoneId) + " were added to free slots on all equipped items");
        else
        {
            SendInfo(admin, maxSocketed + "x " + ChatUtil.Item(manastoneId) + " were added to free slots on all equipped items of player " + player.GetName());
            SendInfo(player, admin.GetName(true) + " added " + count + "x " + ChatUtil.Item(manastoneId) + " to free slots on all your equipped items");
        }
    }

    private void Unsocket(Player admin, Player player)
    {
        foreach (Item targetItem in player.GetEquipment().GetEquippedItemsWithoutStigma())
        {
            if (targetItem.GetItemStonesSize() > 0)
            {
                ItemEquipmentListener.RemoveStoneStats(targetItem.GetItemStones(), player.GetGameStats());
                ItemSocketService.RemoveAllManastone(player, targetItem);
                ItemPacketService.UpdateItemAfterInfoChange(player, targetItem);
                targetItem.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            }
        }
        player.GetGameStats().UpdateStatsVisually();
        if (player == admin)
            SendInfo(player, "Removed manastones from all equipped items.");
        else
        {
            SendInfo(admin, "Removed manastones from all equipped items of player " + player.GetName() + ".");
            SendInfo(player, admin.GetName(true) + " removed all manastones from all your equipped items.");
        }
    }

    private void Enchant(Player admin, Player player, int enchant)
    {
        foreach (Item targetItem in player.GetEquipment().GetEquippedItemsWithoutStigma())
        {
            if (targetItem.GetItemTemplate().IsNoEnchant())
                continue;
            if (targetItem.GetItemTemplate().GetMaxEnchantLevel() == 0 && !targetItem.GetItemTemplate().CanExceedEnchant())
                continue;
            targetItem.SetAmplified(enchant > targetItem.GetItemTemplate().GetMaxEnchantLevel() + targetItem.GetEnchantBonus());
            EnchantService.SetEnchantLevel(player, targetItem, enchant);
        }
        player.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        if (player == admin)
            SendInfo(player, "Enchanted all equipped items to +" + enchant + ".");
        else
        {
            SendInfo(admin, "Enchanted all equipped items of player " + player.GetName() + " to +" + enchant + ".");
            SendInfo(player, admin.GetName(true) + " enchanted all your equipped items to +" + enchant + ".");
        }
    }

    private void Temper(Player admin, Player player, int temperingLevel)
    {
        foreach (Item targetItem in player.GetEquipment().GetEquippedItemsWithoutStigma())
        {
            if (targetItem.GetItemTemplate().GetMaxTampering() > 0)
                TamperingAction.SetTemperingLevel(targetItem, player, Math.Min(temperingLevel, targetItem.GetItemTemplate().GetMaxTampering()));
        }
        player.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        if (player == admin)
            SendInfo(player, "Tempered all equipped items to +" + temperingLevel + ".");
        else
        {
            SendInfo(admin, "Tempered all equipped items of player " + player.GetName() + " to +" + temperingLevel + ".");
            SendInfo(player, admin.GetName(true) + " tempered all your equipped items to +" + temperingLevel + ".");
        }
    }
}
