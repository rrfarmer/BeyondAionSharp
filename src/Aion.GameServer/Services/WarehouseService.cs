using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/WarehouseService (Simple, Luzien). Warehouse expansion + info packets. Integer price→int?; anonymous RequestResponseHandler&lt;Npc&gt;→nested ExpandResponseHandler (captures price); StorageType.X.getId()→GetId(); List.subList(from,to)→GetRange(from, to-from); enum.equals→==; String.valueOf→ToString. Item/Storage/templates/SM_* red-tolerated.</summary>
public class WarehouseService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(WarehouseService));
    private const int MAX_EXPAND = 11;

    /// <summary>Shows Question window and expands on positive response.</summary>
    public static void ExpandWarehouse(Player player, Npc npc)
    {
        StorageExpansionTemplate template = DataManager.WAREHOUSEEXPANDER_DATA.GetWarehouseExpansionTemplate(npc.GetNpcId());
        if (template == null)
        {
            log.LogWarning("Warehouse expansion template could not be found for " + npc);
            return;
        }

        if (!CanExpand(player))
            return;
        int newNpcExpansions = player.GetWhNpcExpands() + 1;
        int minExpansionLevel = template.GetMinExpansionLevel();
        if (newNpcExpansions < minExpansionLevel)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_DUE_TO_MINIMUM_EXTEND_LEVEL_BY_THIS_NPC(npc.GetObjectTemplate().GetL10n(), minExpansionLevel - 1));
            return;
        }
        int? price = template.GetPrice(newNpcExpansions);
        if (price == null || newNpcExpansions > template.GetMaxExpansionLevel())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE_DUE_TO_MAXIMUM_EXTEND_LEVEL_BY_THIS_NPC(npc.GetObjectTemplate().GetL10n(), template.GetMaxExpansionLevel()));
            return;
        }
        RequestResponseHandler<Npc> responseHandler = new ExpandResponseHandler(npc, price.Value);

        bool result = player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_WAREHOUSE_EXPAND_WARNING, responseHandler);
        if (result)
        {
            PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_WAREHOUSE_EXPAND_WARNING, 0, 0, price.Value.ToString()));
        }
    }

    public static void Expand(Player player, bool isNpcExpand)
    {
        if (!CanExpand(player))
            return;
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_SIZE_EXTENDED(8)); // 8 Slots added
        PlayerCommonData pcd = player.GetCommonData();
        if (isNpcExpand)
        {
            pcd.SetWhNpcExpands(pcd.GetWhNpcExpands() + 1);
        }
        else
        {
            pcd.SetWhBonusExpands(pcd.GetWhBonusExpands() + 1);
        }
        player.SetWarehouseLimit();

        SendWarehouseInfo(player, false);
    }

    public static bool CanExpandByTicket(Player player, int ticketLevel)
    {
        if (!CanExpand(player))
            return false;
        if (player.GetWhBonusExpands() - GetCompletedWhQuests(player) >= ticketLevel)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE());
            return false;
        }
        return true;
    }

    public static bool CanExpand(Player player)
    {
        int newExpansions = player.GetWarehouseExpansions() + 1;
        if (newExpansions < 0)
            return false;
        if (newExpansions > MAX_EXPAND)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE());
            return false;
        }
        return true;
    }

    private static int GetCompletedWhQuests(Player player)
    {
        int result = 0;
        QuestStateList qs = player.GetQuestStateList();
        int[] questIds = { 1987, 2985 };
        foreach (int q in questIds)
        {
            if (qs.GetQuestState(q) != null && qs.GetQuestState(q).GetStatus() == QuestStatus.COMPLETE)
                result++;
        }
        return result;
    }

    /// <summary>Sends correctly warehouse packets.</summary>
    public static void SendWarehouseInfo(Player player, bool sendAccountWh)
    {
        List<Item> items = player.GetStorage(StorageType.REGULAR_WAREHOUSE.GetId()).GetItems();

        int whSize = player.GetWarehouseExpansions();
        int itemsSize = items.Count;

        // regular warehouse
        bool firstPacket = true;
        if (itemsSize != 0)
        {
            int index = 0;

            while (index + 10 < itemsSize)
            {
                PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(items.GetRange(index, 10), StorageType.REGULAR_WAREHOUSE.GetId(), whSize,
                    firstPacket, player));
                index += 10;
                firstPacket = false;
            }
            PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(items.GetRange(index, itemsSize - index), StorageType.REGULAR_WAREHOUSE.GetId(), whSize,
                firstPacket, player));
        }

        PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(null, StorageType.REGULAR_WAREHOUSE.GetId(), whSize, false, player));

        if (sendAccountWh)
        {
            // account warehouse
            PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(player.GetStorage(StorageType.ACCOUNT_WAREHOUSE.GetId()).GetItemsWithKinah(),
                StorageType.ACCOUNT_WAREHOUSE.GetId(), 0, true, player));
        }

        PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(null, StorageType.ACCOUNT_WAREHOUSE.GetId(), 0, false, player));
    }

    // Java parity: anonymous RequestResponseHandler<Npc> in expandWarehouse (acceptRequest override).
    private sealed class ExpandResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly int price;

        public ExpandResponseHandler(Npc npc, int price) : base(npc)
        {
            this.price = price;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (responder.GetInventory().TryDecreaseKinah(price))
                Expand(responder, true);
            else
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_WAREHOUSE_EXPAND_NOT_ENOUGH_MONEY()); // warehouse and cube use the same msg..
        }
    }
}
