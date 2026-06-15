using System.Collections.Generic;
using System.Drawing;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Buy (Estrayl). Exchange reward coins for various rewards.</summary>
public class Buy : PlayerCommand
{
    private static readonly Dictionary<int, Dictionary<string, int>> rewards = new();

    public Buy()
        : base("buy", "Exchange your " + ChatUtil.Item(CustomInstanceService.REWARD_COIN_ID) + " for various rewards.")
    {
        SetSyntaxInfo(
            " - Shows all buyable rewards.",
            "<item link|ID> - Buys the respective item."
        );
        InitRewards();
    }

    private void InitRewards()
    {
        AddReward(166000194, 5, 40); // Delta Enchantment Stone
        AddReward(166000195, 5, 50); // Epsilon Enchantment Stone
        AddReward(188051516, 1, 100); // Smart Greater Scroll Bundle
        AddReward(162000107, 30, 100); // Saam King's Herb
        AddReward(162000124, 50, 100); // Superior Recovery Serum
        AddReward(188051868, 1, 50); // [Event] Strongest Inquin Form Candy Pouch
        AddReward(188053610, 3, 150); // [Event] Level 70 Composite Manastone Bundle
        AddReward(188053618, 1, 200); // Honorable Elim's Idian Bundle
        AddReward(166500002, 2, 50); // Amplification Stone
        AddReward(166030005, 2, 80); // Tempering Solution
        AddReward(188053295, 1, 200); // Empyrean Plume Chest
        AddReward(188950015, 1, 400); // Special Courier Pass (Eternal/Lv. 61-65)
        AddReward(188950019, 1, 500); // Special Courier Pass (Mythic/Lv. 61-65)
        AddReward(165020015, 1, 1000); // Armor Wrapping Scroll (Eternal/Lv. 65 and lower)
        AddReward(165020014, 1, 1150); // Weapon Wrapping Scroll (Eternal/Lv. 65 and lower)
        AddReward(165020021, 1, 1350); // Noble Armor Wrapping Scroll (Mythic/Lv. 65 and lower)
        AddReward(165020020, 1, 1500); // Noble Weapon Wrapping Scroll (Mythic/Lv. 65 and lower)
        AddReward(190020175, 1, 1500); // Tahabata Egg
        AddReward(187060103, 1, 1300); // Prestige Wings
        AddReward(169610342, 1, 1000); // [Title] Forgotten Conqueror
        AddReward(190100051, 1, 1200); // Flying Pagati
        AddReward(188053109, 1, 2500); // Ahserion's Equipment Chest
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
            ShowRewards(player);
        else if (paramsArr.Length == 1)
            BuyItem(player, paramsArr[0]);
        else
            SendInfo(player);
    }

    private void ShowRewards(Player player)
    {
        SendInfo(player, "Cost:");
        foreach (KeyValuePair<int, Dictionary<string, int>> idMap in rewards)
        {
            string itemString;
            string cost = idMap.Value["cost"].ToString();
            int amount = idMap.Value["amount"];
            int digitsToPad = 4 - cost.Length;
            if (digitsToPad > 0)
                cost = new string(' ', digitsToPad * 2) + cost;

            if (amount == 1)
                itemString = cost + "   ";
            else
                itemString = cost + "   " + amount + "x ";

            itemString += ChatUtil.Item(idMap.Key);
            SendInfo(player, itemString);
        }
        SendInfo(player, "To buy:");
        SendInfo(player, "1. Right-click an item from the list to your Memo Pad.");
        SendInfo(player, "2. Type " + ChatUtil.Color(GetAliasWithPrefix(), Color.White) + " in your chat field (with space).");
        SendInfo(player, "3. (Ctrl + Right-click) the item from your Memo Pad.");
    }

    private void BuyItem(Player player, string itemLink)
    {
        // redeem item
        int itemId = ChatUtil.GetItemId(itemLink);
        if (DataManager.ITEM_DATA.GetItemTemplate(itemId) == null)
        {
            SendInfo(player, "\"" + itemLink + "\" is not a valid item (must be an item link or ID).");
            return;
        }

        if (!rewards.ContainsKey(itemId))
        {
            SendInfo(player, ChatUtil.Item(itemId) + " is not contained in the rewards.");
            return;
        }
        int cost = rewards[itemId]["cost"];
        long rewardCoins = 0;
        foreach (Item i in player.GetInventory().GetItemsByItemId(CustomInstanceService.REWARD_COIN_ID))
            rewardCoins += i.GetItemCount();

        if (cost > rewardCoins)
        {
            SendInfo(player, "You need " + cost + " to buy " + ChatUtil.Item(itemId) + ".");
            return;
        }

        RequestResponseHandler<Creature> handler = new BuyResponseHandler(this, player, cost, itemId);
        if (player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_AIONJEWEL_SHOP_BUY_CONFIRM, handler))
            PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_AIONJEWEL_SHOP_BUY_CONFIRM, 0, 0, cost,
                ((IL10n)DataManager.ITEM_DATA.GetItemTemplate(CustomInstanceService.REWARD_COIN_ID)).GetL10n()));
    }

    private void AddReward(int itemID, int amount, int cost)
    {
        Dictionary<string, int> val = new Dictionary<string, int>();
        val["cost"] = cost;
        val["amount"] = amount;
        rewards[itemID] = val;
    }

    private sealed class BuyResponseHandler : RequestResponseHandler<Creature>
    {
        private readonly Buy outer;
        private readonly Player player;
        private readonly int cost;
        private readonly int itemId;

        public BuyResponseHandler(Buy outer, Player player, int cost, int itemId)
            : base(null)
        {
            this.outer = outer;
            this.player = player;
            this.cost = cost;
            this.itemId = itemId;
        }

        public override void AcceptRequest(Creature requester, Player responder)
        {
            if (player.GetInventory().DecreaseByItemId(CustomInstanceService.REWARD_COIN_ID, cost))
            {
                outer.SendInfo(player, "You have spent " + cost + ".");
                ItemService.AddItem(player, itemId, rewards[itemId]["amount"], true,
                    new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.DECOMPOSABLE, ItemPacketService.ItemUpdateType.INC_CASH_ITEM));
            }
        }
    }
}
