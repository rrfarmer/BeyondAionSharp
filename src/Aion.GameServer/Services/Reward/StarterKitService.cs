using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Services.Mail;

namespace Aion.GameServer.Services.Reward;

/// <summary>Java parity: services/reward/StarterKitService (Estrayl, AION 4.8). Singleton; LinkedHashMap level→RewardItem list→Dictionary (onLevelUp iterates by level, order-independent); onLevelUp loops fromLevel..toLevel, sends SystemMailService.sendMail per reward (LetterType.EXPRESS). RewardItem/SystemMailService red-tolerated.</summary>
public class StarterKitService
{
    private static readonly StarterKitService INSTANCE = new StarterKitService();
    private readonly Dictionary<int, List<RewardItem>> itemMap = new Dictionary<int, List<RewardItem>>();

    public static StarterKitService GetInstance()
    {
        return INSTANCE;
    }

    private StarterKitService()
    {
        itemMap[1] = new List<RewardItem>();
        itemMap[20] = new List<RewardItem>();
        itemMap[25] = new List<RewardItem>();
        itemMap[35] = new List<RewardItem>();
        itemMap[50] = new List<RewardItem>();
        itemMap[60] = new List<RewardItem>();

        itemMap[1].Add(new RewardItem(169610056, 1)); // [Title Card] Novice of Atreia – 30-day pass
        itemMap[20].Add(new RewardItem(188054100, 1)); // Bronze Coin Box
        itemMap[20].Add(new RewardItem(125001832, 1)); // Experienced Lepharist Veil
        itemMap[20].Add(new RewardItem(122000449, 1)); // Ghost Rose Quartz Ring
        itemMap[20].Add(new RewardItem(122000451, 1)); // Ghost Crystal Ring
        itemMap[20].Add(new RewardItem(120015052, 1)); // Prestigious Magic Earrings
        itemMap[20].Add(new RewardItem(120015051, 1)); // Prestigious Combat Earrings
        itemMap[20].Add(new RewardItem(123000879, 1)); // Morai's Belt
        itemMap[25].Add(new RewardItem(190100032, 1)); // Pagati Ironhide
        itemMap[25].Add(new RewardItem(164002272, 25)); // [Event] Enduring Greater Raging Wind Scroll
        itemMap[25].Add(new RewardItem(162000039, 25)); // Divine Wind Serum
        itemMap[25].Add(new RewardItem(162002018, 25)); // [Event] Wormwood Dish
        itemMap[35].Add(new RewardItem(188054101, 1)); // Silver Coin Box
        itemMap[35].Add(new RewardItem(169620082, 1)); // Gathering Boost Charm II - 100%
        itemMap[35].Add(new RewardItem(169620094, 1)); // Crafting Boost Charm III - 100%
        itemMap[50].Add(new RewardItem(121000815, 1)); // Lonely Diamond Necklace
        itemMap[50].Add(new RewardItem(120000901, 1)); // Lonely Diamond Earrings
        itemMap[50].Add(new RewardItem(122001038, 1)); // Lonely Diamond Ring
        itemMap[50].Add(new RewardItem(188053624, 10)); // Return Scroll Bundle
        itemMap[50].Add(new RewardItem(161001001, 5)); // Revival Stone
        itemMap[60].Add(new RewardItem(169620072, 1)); // AP Boost Charm II - 30%
        itemMap[60].Add(new RewardItem(162002030, 100)); // Event] Premium Restoration Serum
        itemMap[60].Add(new RewardItem(162002018, 50)); // [Event] Wormwood Dish
        itemMap[60].Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
        itemMap[60].Add(new RewardItem(188053783, 5)); // Stigma Sack
    }

    public void OnLevelUp(Player player, int fromLevel, int toLevel)
    {
        for (int level = fromLevel; level <= toLevel; level++)
        {
            if (!itemMap.ContainsKey(level))
                continue;
            foreach (RewardItem e in itemMap[level])
            {
                SystemMailService.SendMail("Beyond Aion", player.GetName(), "Starter Kit",
                    "Greetings Daeva!\n\n"
                        + "In gratitude for your decision to join our server, we would like to support you with an additional item pack during the leveling.\n\n"
                        + "Enjoy your stay on Beyond Aion!",
                    e.GetId(), e.GetCount(), 0, LetterType.EXPRESS);
            }
        }
    }
}
