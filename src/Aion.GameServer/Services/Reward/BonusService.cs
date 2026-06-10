using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Itemgroups;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Services.Reward;

/// <summary>Java parity: services/reward/BonusService (Rolandas, Pad, Neon). Static; getQuestBonus→Chance.selectElement of random matching group→QuestItems; getMatchingItemsOfRandomGroup loops remaining groups (Chance.selectElement remove=true) filtering by race/quest; getBonusGroups switch BonusType→ITEM_GROUPS_DATA accessor (commented GATHER/BOSS/ENCHANT preserved; MOVIE/NONE break; default inline warn); List&lt;? extends ItemRaceEntry&gt;→List&lt;ItemRaceEntry&gt; (wildcard erase); streams→LINQ; getType→GetType_. Chance/BonusItemGroup/ItemRaceEntry red-tolerated.</summary>
public class BonusService
{
    private BonusService()
    {
    }

    public static QuestItems GetQuestBonus(Player player, QuestTemplate questTemplate)
    {
        if (questTemplate.GetBonus() == null)
            return null;

        List<ItemRaceEntry> itemsOfRandomGroup = GetMatchingItemsOfRandomGroup(player, questTemplate);
        if (itemsOfRandomGroup == null)
            return null;

        ItemRaceEntry item = Chance.SelectElement(itemsOfRandomGroup);
        return item == null ? null : new QuestItems(item.GetId(), item.GetCount());
    }

    public static List<ItemRaceEntry> GetMatchingItemsOfRandomGroup(Player player, QuestTemplate questTemplate)
    {
        List<BonusItemGroup> remainingGroups = new List<BonusItemGroup>(GetBonusGroups(questTemplate.GetBonus().GetType_()));
        List<ItemRaceEntry> allRewards = null;

        while (remainingGroups.Count != 0)
        {
            BonusItemGroup group = Chance.SelectElement(remainingGroups, true);
            if (group == null)
                break;
            allRewards = group.GetItems().Where(i => i.Matches(player.GetRace(), questTemplate)).Cast<ItemRaceEntry>().ToList();
            if (allRewards.Count != 0)
                break;
        }
        return allRewards;
    }

    private static List<BonusItemGroup> GetBonusGroups(BonusType type)
    {
        switch (type)
        {
            // case GATHER:
            // return DataManager.ITEM_GROUPS_DATA.getGatherGroups();
            // case BOSS:
            // return DataManager.ITEM_GROUPS_DATA.getBossGroups();
            // case ENCHANT:
            // return DataManager.ITEM_GROUPS_DATA.getEnchantGroups();
            case BonusType.EVENTS:
                return DataManager.ITEM_GROUPS_DATA.GetEventGroups();
            case BonusType.FOOD:
                return DataManager.ITEM_GROUPS_DATA.GetFoodGroups();
            case BonusType.MANASTONE:
                return DataManager.ITEM_GROUPS_DATA.GetManastoneGroups();
            case BonusType.MEDICINE:
                return DataManager.ITEM_GROUPS_DATA.GetMedicineGroups();
            case BonusType.MEDAL:
                return DataManager.ITEM_GROUPS_DATA.GetMedalGroups();
            case BonusType.TASK:
                return DataManager.ITEM_GROUPS_DATA.GetCraftGroups();
            case BonusType.MOVIE:
            case BonusType.NONE:
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(nameof(BonusService)).LogWarning("Bonus of type " + type + " is not implemented");
                break;
        }
        return new List<BonusItemGroup>();
    }
}
