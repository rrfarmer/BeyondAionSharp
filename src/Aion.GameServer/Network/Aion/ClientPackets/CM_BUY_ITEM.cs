using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BUY_ITEM (orz, ATracer, Simple, xTz). Buy/sell/repurchase against private store, shop, abyss/reward shop, or merchant pet. TradeService/PrivateStoreService/RepurchaseService red-tolerated.</summary>
public class CM_BUY_ITEM : AionClientPacket
{
    private int sellerObjId;
    private TradeAction? tradeAction;
    private bool isAudit;
    private TradeList tradeList;
    private RepurchaseList repurchaseList;

    public CM_BUY_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        sellerObjId = ReadD();
        tradeAction = FromId(ReadUC());
        ReadC(); // unused
        int amount = ReadUH(); // total no of items

        if (tradeAction == null)
            return;
        if (amount > 36)
        {
            isAudit = true;
            AuditLogger.Log(player, "might be abusing CM_BUY_ITEM amount: " + amount);
            return;
        }
        if (tradeAction == TradeAction.SHOP_REPURCHASE)
        {
            repurchaseList = new RepurchaseList(sellerObjId);
        }
        else
        {
            tradeList = new TradeList(sellerObjId);
        }

        var entryIds = new HashSet<int>();
        for (int i = 0; i < amount; i++)
        {
            int entryId = ReadD();
            long count = ReadQ();
            EntryType entryType = GetEntryType(tradeAction.Value);

            // prevent exploit packets
            if (count <= 0 || (entryId <= 0 && entryType != EntryType.STORE_INDEX) || count > 20000)
            {
                isAudit = true;
                AuditLogger.Log(player, "might be abusing CM_BUY_ITEM entry: " + entryType + " " + entryId + " was sent with count " + count + " for " + tradeAction);
                break;
            }
            if (entryType == EntryType.STORE_INDEX && entryId < 0)
            {
                isAudit = true;
                AuditLogger.Log(player, "might be abusing CM_BUY_ITEM: " + entryType + " " + entryId + " was sent for " + tradeAction);
                break;
            }
            if ((entryType == EntryType.ITEM_OBJECT_ID || entryType == EntryType.STORE_INDEX) && !entryIds.Add(entryId))
            {
                isAudit = true;
                AuditLogger.Log(player, "might be abusing CM_BUY_ITEM: " + entryType + " " + entryId + " was sent more than once for " + tradeAction);
                break;
            }
            if (tradeAction == TradeAction.SHOP_REPURCHASE)
                repurchaseList.AddRepurchaseItem(player, entryId, count);
            else
                tradeList.AddItem(entryId, count);
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (tradeAction == null || isAudit || player == null)
            return;

        VisibleObject target = player.GetKnownList().GetObject(sellerObjId);

        if (target == null)
            return;

        if (target is Player targetPlayer && tradeAction == TradeAction.PRIVATE_STORE_BUY)
        {
            PrivateStoreService.SellStoreItem(targetPlayer, player, tradeList);
        }
        else if (target is Npc npc)
        {
            TradeWithNpc(player, npc);
        }
        else if (target is Pet pet && pet.GetMaster() == player && tradeAction == TradeAction.PET_SELL)
        {
            PetFunction pf = pet.GetObjectTemplate().GetPetFunction(PetFunctionType.MERCHANT);
            if (pf != null)
            {
                TradeService.PerformSellToShop(player, tradeList, null, pf.GetRatePrice());
            }
        }
    }

    private void TradeWithNpc(Player player, Npc npc)
    {
        if (!global::Aion.GameServer.Utils.PositionUtil.IsInTalkRange(player, npc))
        {
            AuditLogger.Log(player, "might be abusing CM_BUY_ITEM: too far from " + npc + " at " + player.GetPosition());
            return;
        }
        if (!DialogService.IsInteractionAllowed(player, npc))
        {
            AuditLogger.Log(player, "might be abusing CM_BUY_ITEM: no right trading with " + npc);
            return;
        }
        switch (tradeAction)
        {
            case TradeAction.SHOP_SELL:
                if (npc.CanBuy() || npc.CanPurchase())
                {
                    TradeListTemplate template = DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(npc.GetNpcId());
                    if (template != null && template.GetTradeNpcType() == TradeNpcType.ABYSS)
                        TradeService.PerformSellForAPToShop(player, tradeList, template);
                    else
                        TradeService.PerformSellToShop(player, tradeList, template);
                }
                break;
            case TradeAction.SHOP_REPURCHASE:
                if (npc.CanBuy()) RepurchaseService.GetInstance().RepurchaseFromShop(player, repurchaseList);
                break;
            case TradeAction.SHOP_BUY:
            case TradeAction.ABYSS_SHOP_BUY:
            case TradeAction.REWARD_SHOP_BUY:
            case TradeAction.ABYSS_KINAH_SHOP_BUY:
                if (npc.CanSell()) TradeService.BuyFromShop(npc, player, tradeList);
                break;
        }
    }

    private enum TradeAction
    {
        PRIVATE_STORE_BUY = 0, SHOP_SELL = 1, SHOP_REPURCHASE = 2, SHOP_BUY = 13,
        ABYSS_SHOP_BUY = 14, REWARD_SHOP_BUY = 15, ABYSS_KINAH_SHOP_BUY = 16, PET_SELL = 17
    }
    private enum EntryType { STORE_INDEX, ITEM_ID, ITEM_OBJECT_ID }
    private static TradeAction? FromId(int id) => id is 0 or 1 or 2 or 13 or 14 or 15 or 16 or 17 ? (TradeAction)id : null;
    private static EntryType GetEntryType(TradeAction action) => action switch
    {
        TradeAction.PRIVATE_STORE_BUY => EntryType.STORE_INDEX,
        TradeAction.SHOP_SELL or TradeAction.SHOP_REPURCHASE or TradeAction.PET_SELL => EntryType.ITEM_OBJECT_ID,
        _ => EntryType.ITEM_ID
    };
}
