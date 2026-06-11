using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Model.Templates.TradeList;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BUY_ITEM (orz, ATracer, Simple, xTz). Buy/sell/repurchase against private store, shop, abyss/reward shop, or merchant pet. TradeService/PrivateStoreService/RepurchaseService red-tolerated.</summary>
public class CM_BUY_ITEM : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_BUY_ITEM));
    private int sellerObjId;
    private short tradeActionId;
    private int amount;
    private int itemId;
    private long count;
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
        tradeActionId = ReadH();
        amount = ReadUH(); // total no of items

        if (amount < 0 || amount > 36)
        {
            isAudit = true;
            AuditLogger.Log(player, "might be abusing CM_BUY_ITEM amount: " + amount);
            return;
        }
        if (tradeActionId == 2)
        {
            repurchaseList = new RepurchaseList(sellerObjId);
        }
        else
        {
            tradeList = new TradeList(sellerObjId);
        }

        for (int i = 0; i < amount; i++)
        {
            itemId = ReadD();
            count = ReadQ();

            // prevent exploit packets
            if (count < 0 || (itemId <= 0 && tradeActionId != 0) || count > 20000)
            {
                isAudit = true;
                AuditLogger.Log(player, "might be abusing CM_BUY_ITEM item: " + itemId + " count: " + count);
                break;
            }

            switch (tradeActionId)
            {
                case 0:// private store (in this case its not itemId/objId, but item index in sellers list...)
                case 1:// sell to shop
                case 13:// buy from shop
                case 14:// buy from abyss shop
                case 15:// buy from reward shop
                case 16:// buy from general shop
                case 17:// sell to pet
                    tradeList.AddItem(itemId, count);
                    break;
                case 2:// repurchase
                    repurchaseList.AddRepurchaseItem(player, itemId, count);
                    break;
            }
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (isAudit || player == null)
            return;

        VisibleObject target = player.GetKnownList().GetObject(sellerObjId);

        if (target == null)
            return;

        if (target is Player targetPlayer && tradeActionId == 0)
        {
            PrivateStoreService.SellStoreItem(targetPlayer, player, tradeList);
        }
        else if (target is Npc npc)
        {
            if (!DialogService.IsInteractionAllowed(player, npc))
            {
                AuditLogger.Log(player, "might be abusing CM_BUY_ITEM: no right trading with " + npc);
                return;
            }
            TradeListTemplate tradeTemplate;
            switch (tradeActionId)
            {
                case 1: // sell to shop
                    if (npc.CanBuy() || npc.CanPurchase())
                    {
                        tradeTemplate = DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(npc.GetNpcId());
                        if (tradeTemplate != null && tradeTemplate.GetTradeNpcType() == TradeNpcType.ABYSS)
                            TradeService.PerformSellForAPToShop(player, tradeList, tradeTemplate);
                        else
                            TradeService.PerformSellToShop(player, tradeList, tradeTemplate);
                    }
                    break;
                case 2: // repurchase
                    if (npc.CanBuy())
                        RepurchaseService.GetInstance().RepurchaseFromShop(player, repurchaseList);
                    break;
                case 13: // buy from shop
                case 14: // buy from abyss shop
                case 15: // reward shop
                case 16: // abyss_kinah shop
                    if (npc.CanSell())
                        TradeService.PerformBuyFromShop(npc, player, tradeList);
                    break;
                default:
                    log.LogWarning("Unknown shop action: " + tradeActionId);
                    break;
            }
        }
        else if (target is Pet)
        {
            PetFunction pf = ((Pet)target).GetObjectTemplate().GetPetFunction(PetFunctionType.MERCHANT);
            if (pf != null && tradeActionId == 17)
            {
                TradeService.PerformSellToShop(player, tradeList, null, pf.GetRatePrice());
            }
        }
    }
}
