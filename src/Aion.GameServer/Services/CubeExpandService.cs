using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ItemUpdateType = Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/CubeExpandService (ATracer, Simple, Luzien).</summary>
public class CubeExpandService
{
    private static readonly ILogger log = NullLogger.Instance;

    /// <summary>Shows Question window and expands on positive response.</summary>
    public static void ExpandCube(Player player, Npc npc)
    {
        StorageExpansionTemplate template = DataManager.CUBEEXPANDER_DATA.GetCubeExpansionTemplate(npc.GetNpcId());
        if (template == null)
        {
            log.LogWarning("Cube expansion template could not be found for " + npc);
            return;
        }

        if (!CanExpand(player))
            return;
        int newNpcExpansions = player.GetNpcExpands() + 1;
        int minExpansionLevel = template.GetMinExpansionLevel();
        if (newNpcExpansions < minExpansionLevel)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.InventoryCantExtendBelowNpcMinimum(npc.GetObjectTemplate().GetL10n(), minExpansionLevel - 1));
            return;
        }
        int? price = template.GetPrice(newNpcExpansions);
        int maxExpansionLevel = Math.Min(template.GetMaxExpansionLevel(), CustomConfig.NPC_CUBE_EXPANDS_SIZE_LIMIT);
        if (price == null || newNpcExpansions > maxExpansionLevel)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.InventoryCantExtendAboveNpcMaximum(npc.GetObjectTemplate().GetL10n(), maxExpansionLevel));
            return;
        }
        RequestResponseHandler<Npc> responseHandler = new CubeExpandResponseHandler(npc, price.Value);
        bool result = player.GetResponseRequester().PutRequest(SmQuestionWindow.WarehouseExpandWarning, responseHandler);
        if (result)
        {
            PacketSendUtility.SendPacket(player, new SmQuestionWindow(SmQuestionWindow.WarehouseExpandWarning, 0, 0, price.Value.ToString()));
        }
    }

    // Java parity: anonymous RequestResponseHandler<Npc> capturing the (effectively final) price.
    private class CubeExpandResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly int price;

        internal CubeExpandResponseHandler(Npc npc, int price)
            : base(npc)
        {
            this.price = price;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (responder.GetInventory().TryDecreaseKinah(price, ItemUpdateType.DEC_KINAH_CUBE))
                NpcExpand(responder);
            else
                PacketSendUtility.SendPacket(responder, SmSystemMessage.WarehouseExpandNotEnoughMoney()); // warehouse and cube use the same msg..
        }
    }

    /// <summary>
    /// Expands the cubes.
    /// </summary>
    /// <param name="type">1 - npc // 2 - item // 3 - quest</param>
    private static void Expand(Player player, int type)
    {
        if (!CanExpand(player))
            return;
        PacketSendUtility.SendPacket(player, SmSystemMessage.InventorySizeExtended(9));
        switch (type)
        {
            case 1: // npc
                player.GetCommonData().SetNpcExpands(player.GetNpcExpands() + 1);
                break;
            case 2: // item
                player.GetCommonData().SetItemExpands(player.GetItemExpands() + 1);
                break;
            case 3: // quest
                player.GetCommonData().SetQuestExpands(player.GetQuestExpands() + 1);
                break;
        }
        player.SetCubeLimit();
        PacketSendUtility.SendPacket(player, SmCubeUpdate.CubeSize(player));
    }

    public static void QuestExpand(Player player)
    {
        Expand(player, 3);
    }

    public static void ItemExpand(Player player)
    {
        Expand(player, 2);
    }

    public static void NpcExpand(Player player)
    {
        Expand(player, 1);
    }

    public static bool CanExpandByTicket(Player player, int ticketLevel)
    {
        if (!CanExpand(player))
            return false;
        if (player.GetItemExpands() >= ticketLevel)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.InventoryCantExtendMore());
            return false;
        }
        return true;
    }

    public static bool CanExpand(Player player)
    {
        int newExpansions = player.GetNpcExpands() + player.GetQuestExpands() + player.GetItemExpands() + 1;
        if (newExpansions < 0)
            return false;
        if (newExpansions > CustomConfig.CUBE_EXPANSION_LIMIT)
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.InventoryCantExtendMore());
            return false;
        }
        return true;
    }
}
