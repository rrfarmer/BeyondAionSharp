using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Utils;
using ItemUpdateType = Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services.Item;

/// <summary>Java parity: services/item/ItemChargeService (ATracer). Equipment charging (Improvement chargeWay 1=kinah/2=AP): filterItemsToCondition, startChargingEquippedItems (confirm window + payment), chargeItems/chargeItem (rank-gated level, charge points, stat update), pay-amount math (level ratios), payment processing. Collections.singletonList->new List; stream.filter.collect->LINQ; switch-expr->C# switch-expr; anonymous RequestResponseHandler->nested ChargeRequestHandler; Math.round->Floor(x+0.5); Math.ceil->Ceiling; IllegalArgument->Argument. ChargeInfo/Improvement/SM_ red-tolerated.</summary>
public class ItemChargeService
{
    public static List<Item> FilterItemsToCondition(Player player, Item selectedItem, int chargeWay)
    {
        if (selectedItem != null)
            return new List<Item> { selectedItem };
        return player.GetEquipment().GetEquippedItems().Where(item => item.CalculateAvailableChargeLevel(player) != 0
            && item.GetImprovement() != null && item.GetImprovement().GetChargeWay() == chargeWay && item.GetChargePoints() < ChargeInfo.LEVEL2)
            .ToList();
    }

    public static void StartChargingEquippedItems(Player player, int senderObj, int chargeWay)
    {
        // TODO: Check this : SM_QUESTION_WINDOW.STR_ITEM_CHARGE_CONFIRM_SOME_ALREADY_CHARGED !!!
        ICollection<Item> filteredItems = FilterItemsToCondition(player, null, chargeWay);
        if (filteredItems.Count == 0)
        {
            if (chargeWay == 1)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_ALL_FAIL_NO_CHARGEABLE_EQUIPMENT());
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_ALL_FAIL_NO_CHARGEABLE_EQUIPMENT());
            return;
        }

        long payAmount = CalculatePrice(filteredItems, player);
        RequestResponseHandler<Player> request = new ChargeRequestHandler(player, chargeWay, payAmount, filteredItems);
        int msg = chargeWay == 1 ? SM_QUESTION_WINDOW.STR_ITEM_CHARGE_ALL_CONFIRM : SM_QUESTION_WINDOW.STR_ITEM_CHARGE2_ALL_CONFIRM;
        if (player.GetResponseRequester().PutRequest(msg, request))
            PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(msg, senderObj, 0, payAmount.ToString()));
    }

    private static long CalculatePrice(ICollection<Item> items, Player player)
    {
        long result = 0;
        foreach (Item item in items)
            result += GetPayAmountForService(item, item.CalculateAvailableChargeLevel(player));
        return result;
    }

    public static void ChargeItems(Player player, ICollection<Item> items, int maxLevel, bool ignoreRankRequirement, bool requirePayment)
    {
        if (items.Count == 0)
            return;
        HashSet<int> chargeWays = new HashSet<int>(2);
        bool itemsUpdated = false;
        foreach (Item item in items)
        {
            if (ChargeItem(player, item, maxLevel, ignoreRankRequirement, requirePayment))
            {
                itemsUpdated = true;
                chargeWays.Add(item.GetImprovement().GetChargeWay());
            }
        }
        if (!itemsUpdated)
            return;
        foreach (int chargeWay in chargeWays)
        {
            if (chargeWay == 1)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_ALL_COMPLETE());
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_ALL_COMPLETE());
        }
    }

    public static bool ChargeItem(Player player, Item item, int maxLevel, bool ignoreRankRequirement, bool requirePayment)
    {
        Improvement improvement = item.GetImprovement();
        if (improvement == null)
            return false;

        int level = ignoreRankRequirement ? maxLevel : CalculateMaxChargeLevelBasedOnRank(player, item, maxLevel);
        if (level <= 0)
            return false;
        int maxChargePoints = level == 1 ? ChargeInfo.LEVEL1 : ChargeInfo.LEVEL2;
        int chargePointsToAdd = Math.Max(0, maxChargePoints - item.GetChargePoints());
        // process payment if needed
        if (chargePointsToAdd <= 0 || requirePayment && !ProcessPayment(player, item, level))
            return false;

        if (item.GetConditioningInfo().UpdateChargePoints(chargePointsToAdd))
            PacketSendUtility.SendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, item, ItemUpdateType.CHARGE));

        if (improvement.GetChargeWay() == 1)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_SUCCESS(item.GetL10n(), level));
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_SUCCESS(item.GetL10n(), level));
        }
        player.GetGameStats().UpdateStatsVisually();
        return true;
    }

    public static bool ProcessPayment(Player player, Item item, int level)
    {
        return ProcessPayment(player, item.GetImprovement().GetChargeWay(), GetPayAmountForService(item, level));
    }

    public static bool ProcessPayment(Player player, int chargeWay, long amount)
    {
        return chargeWay switch
        {
            1 => ProcessKinahPayment(player, amount),
            2 => ProcessAPPayment(player, amount),
            _ => false,
        };
    }

    public static bool ProcessKinahPayment(Player player, long requiredKinah)
    {
        return player.GetInventory().TryDecreaseKinah(requiredKinah);
    }

    public static bool ProcessAPPayment(Player player, long requiredAP)
    {
        if (player.GetAbyssRank().GetAp() < requiredAP)
            return false;
        AbyssPointsService.AddAp(player, (int)-requiredAP);
        return true;
    }

    public static long GetPayAmountForService(Item item, int chargeLevel)
    {
        Improvement improvement = item.GetImprovement();
        if (improvement == null)
            return 0;
        int price1 = improvement.GetPrice1();
        int price2 = improvement.GetPrice2();
        double firstLevel = price1 / 2d;
        double updateLevel = Math.Floor(firstLevel + (price2 - price1) / 2d + 0.5);
        double money = 0;
        float currentChargeRatio = 1f;
        switch (chargeLevel)
        {
            case 1:
                currentChargeRatio -= ((float)item.GetChargePoints() / (float)ChargeInfo.LEVEL1);
                money = Math.Ceiling(firstLevel * currentChargeRatio);
                break;
            case 2:
                switch (GetNextChargeLevel(item))
                {
                    case 1:
                        {
                            // full
                            currentChargeRatio -= (((float)item.GetChargePoints() / (float)ChargeInfo.LEVEL1));
                            money = Math.Ceiling(firstLevel * currentChargeRatio) + updateLevel;
                            break;
                        }
                    case 2:
                        {
                            // update
                            currentChargeRatio -= (((float)(item.GetChargePoints() - ChargeInfo.LEVEL1) / (float)(ChargeInfo.LEVEL2 - ChargeInfo.LEVEL1)));
                            money = Math.Ceiling(updateLevel * currentChargeRatio);
                            break;
                        }
                }
                break;
        }
        return Math.Max(0, (long)money);
    }

    private static int GetNextChargeLevel(Item item)
    {
        int charge = item.GetChargePoints();
        if (charge < ChargeInfo.LEVEL1)
            return 1;
        if (charge < ChargeInfo.LEVEL2)
            return 2;
        throw new ArgumentException("Invalid charge level " + charge);
    }

    public static int CalculateMaxChargeLevelBasedOnRank(Player player, Item item, int maxChargeLevel)
    {
        return Math.Min(item.CalculateAvailableChargeLevel(player), maxChargeLevel);
    }

    private sealed class ChargeRequestHandler : RequestResponseHandler<Player>
    {
        private readonly Player player;
        private readonly int chargeWay;
        private readonly long payAmount;
        private readonly ICollection<Item> filteredItems;

        public ChargeRequestHandler(Player player, int chargeWay, long payAmount, ICollection<Item> filteredItems)
            : base(player)
        {
            this.player = player;
            this.chargeWay = chargeWay;
            this.payAmount = payAmount;
            this.filteredItems = filteredItems;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            if (ProcessPayment(player, chargeWay, payAmount))
                ChargeItems(player, filteredItems, 2, false, false);
        }
    }
}
