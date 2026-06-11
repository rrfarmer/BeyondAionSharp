using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Services.Reward;

/// <summary>Java parity: services/reward/AdventService (Nathan, Estrayl, Neon, Sykra) — Solorius advent calendar. Singleton; rewards map (computeIfAbsent→TryGetValue); LocalDate→DateOnly (ServerTime.now().toLocalDate→DateOnly.FromDateTime); Month.DECEMBER→12; java.awt.Color.PINK→System.Drawing.Color.Pink; streams map/filter/count→LINQ; iterator hasNext trailing-comma→index test; AdventDAO/ChatProcessor/ChatUtil red-tolerated.</summary>
public class AdventService
{
    private static readonly AdventService instance = new AdventService();
    private readonly Dictionary<int, List<RewardItem>> rewards = new Dictionary<int, List<RewardItem>>();

    private AdventService()
    {
        AddReward(1, 170190034, 1); // [Event] Solorius Cake
        AddReward(2, 125040166, 1); // Solorius Hairpin/Top Hat
        AddReward(3, 186000237, 1000); // Ancient Coin
        AddReward(4, 188051879, 1); // Solorius Furniture Set Box
        AddReward(5, 166100011, 600); // Greater Supplements (Mythic)
        AddReward(6, 190020236, 1); // Mini Hyperion Egg (30 days)
        AddReward(7, 188051297, 1); // 12 Solorius Inquin Form Candy (Elyos)
        AddReward(7, 188051298, 1); // 12 Solorius Inquin Form Candy (Asmodians)
        AddReward(8, 166020003, 10); // [Event] Omega Enchantment Stone
        AddReward(9, 170390016, 1); // Solorius Garden Tree (Elyos)
        AddReward(9, 170395016, 1); // Solorius Garden Tree (Asmodians)
        AddReward(10, 160010201, 25); // [Event] Solorius Cookie
        AddReward(11, 162001057, 5); // Tea of Repose - 100% Recovery
        AddReward(12, 188050004, 5); // Red Solorius Stocking (Elyos)
        AddReward(12, 188050007, 5); // Red Solorius Stocking (Asmodians)
        AddReward(13, 110900665, 1); // Resplendent Jolly Coat
        AddReward(14, 166030007, 5); // [Event] Tempering Solution
        AddReward(15, 188054014, 5); // [Event] Lunahare Kisk Box
        AddReward(16, 164002167, 25); // [Event] Drana Coffee
        AddReward(17, 188051879, 1); // Solorius Furniture Set Box
        AddReward(18, 188051299, 1); // 12 Solorius Tiger Form Candy (Elyos)
        AddReward(18, 188051300, 1); // 12 Solorius Tiger Form Candy (Asmodians)
        AddReward(19, 186000143, 250); // Kahrun's Symbol
        AddReward(20, 162002018, 20); // [Event] Wormwood Dish
        AddReward(21, 188050006, 5); // Green Solorius Stocking (Elyos)
        AddReward(21, 188050009, 5); // Green Solorius Stocking (Asmodians)
        AddReward(22, 166500005, 5); // [Event] Amplification Stone
        AddReward(23, 160010201, 25); // [Event] Solorius Cookie
        AddReward(24, 190020109, 1); // Solorinerk Egg
        AddReward(24, 188053610, 5); // [Event] Level 70 Composite Manastone Bundle
        AddReward(24, 166150019, 5); // Assured Greater Felicitous Socketing (Mythic)
    }

    private void AddReward(int day, int itemId, long itemCount)
    {
        if (!rewards.TryGetValue(day, out List<RewardItem> list))
        {
            list = new List<RewardItem>();
            rewards[day] = list;
        }
        list.Add(new RewardItem(itemId, itemCount));
    }

    public void OnLogin(Player player)
    {
        if (!EventsConfig.ENABLE_ADVENT_CALENDAR)
            return;
        DateOnly today = DateOnly.FromDateTime(ServerTime.Now().Date);
        if (!IsAdventSeason(today))
            return;
        if (!ChatProcessor.GetInstance().IsCommandAllowed(player, "advent"))
            return;
        int day = today.Day;
        if (!rewards.ContainsKey(day) || rewards[day].Count == 0 || !AdventDAO.CanReceiveReward(player, today))
            return;
        PacketSendUtility.SendMessage(player,
            "You can open your advent calendar door for today!" + "\nType in .advent to redeem todays reward on this character.\n"
                + ChatUtil.Color("ATTENTION:", Color.Pink) + " Only one character per account can receive this reward!");
    }

    public bool IsAdventSeason()
    {
        return IsAdventSeason(DateOnly.FromDateTime(ServerTime.Now().Date));
    }

    private bool IsAdventSeason(DateOnly date)
    {
        return date.Month == 12 && date.Day <= 24; // Month.DECEMBER
    }

    public void RedeemReward(Player player)
    {
        DateOnly today = DateOnly.FromDateTime(ServerTime.Now().Date);
        int day = today.Day;
        List<RewardItem> todaysRewards = rewards.GetValueOrDefault(day);

        if (!IsAdventSeason(today) || todaysRewards == null || todaysRewards.Count == 0)
        {
            PacketSendUtility.SendMessage(player, "There is no advent calendar door for today.");
            return;
        }

        if (!AdventDAO.CanReceiveReward(player, today))
        {
            PacketSendUtility.SendMessage(player, "You have already opened today's advent calendar door on this account.");
            return;
        }

        long regularCubeItems = todaysRewards
            .Select(r => DataManager.ITEM_DATA.GetItemTemplate(r.GetId()))
            .Where(r => r.GetExtraInventoryId() <= 0)
            .Where(r => r.GetRace() != player.GetOppositeRace())
            .Count();
        if (player.GetInventory().GetFreeSlots() < regularCubeItems)
        {
            PacketSendUtility.SendMessage(player, "You don't have enough free slots in your inventory.");
            return;
        }

        if (!AdventDAO.StoreLastReceivedDay(player, today))
        {
            PacketSendUtility.SendMessage(player, "Sorry. Some shugo broke our database, please report this in our bugtracker :(");
            return;
        }

        foreach (RewardItem item in todaysRewards)
        {
            if (DataManager.ITEM_DATA.GetItemTemplate(item.GetId()).GetRace() == player.GetOppositeRace())
                continue;
            ItemService.AddItem(player, item.GetId(), item.GetCount(), true);
        }
    }

    public void ShowTodaysReward(Player player)
    {
        DateOnly today = DateOnly.FromDateTime(ServerTime.Now().Date);
        List<RewardItem> todaysRewards = rewards.GetValueOrDefault(today.Day);
        if (today.Month != 12 || todaysRewards == null || todaysRewards.Count == 0) // Month.DECEMBER
        {
            PacketSendUtility.SendMessage(player, "There is no advent calendar door for today.");
            return;
        }

        StringBuilder sb = new StringBuilder("Today's advent calendar reward(s):\n");

        for (int idx = 0; idx < todaysRewards.Count; idx++)
        {
            int id = todaysRewards[idx].GetId();
            ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(id);
            if (template != null && template.GetRace() == player.GetOppositeRace())
                continue;
            sb.Append(ChatUtil.Item(id)).Append(idx < todaysRewards.Count - 1 ? ", " : "");
        }
        PacketSendUtility.SendMessage(player, sb.ToString());
    }

    public static AdventService GetInstance()
    {
        return instance;
    }
}
