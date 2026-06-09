using System;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/ItemRaceEntry. implements Chance→IChance; @XmlSeeAlso→[XmlInclude].</summary>
[XmlType("ItemRaceEntry")]
[XmlInclude(typeof(IdLevelReward))]
public class ItemRaceEntry : IChance
{
    [XmlAttribute("id")] private int id;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent). StaticDataListener (Unmarshaller-keyed) has no C# analog → DataManager.ITEM_DATA.
    public void AfterUnmarshal(object parent)
    {
        ItemData itemData = DataManager.ITEM_DATA;
        ItemTemplate itemTemplate = itemData.GetItemTemplate(id);
        if (itemTemplate == null)
            throw new ArgumentException("BonusItemGroup item ID " + id + " is invalid");
        if (itemTemplate.GetRace() != Race.PC_ALL && race != Race.PC_ALL && itemTemplate.GetRace() != race)
            throw new ArgumentException("BonusItemGroup item " + id + " has invalid race " + race + ". Item is only for " + itemTemplate.GetRace());
    }

    public int GetId()
    {
        return id;
    }

    public Race GetRace()
    {
        return race;
    }

    public virtual long GetCount()
    {
        return 1L;
    }

    public virtual float GetChance()
    {
        return 100f;
    }

    public bool Matches(Race playerRace, QuestTemplate questTemplate)
    {
        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(id);
        if (!MatchesRace(itemTemplate, playerRace))
            return false;
        if (!MatchesLevel(itemTemplate, questTemplate.GetBonus().GetLevel()))
            return false;
        if (!MatchesQuest(questTemplate))
            return false;
        return true;
    }

    protected virtual bool MatchesQuest(QuestTemplate questTemplate)
    {
        return true;
    }

    protected virtual bool MatchesLevel(ItemTemplate itemTemplate, int bonusItemLevel)
    {
        return bonusItemLevel == 0 || bonusItemLevel == itemTemplate.GetLevel();
    }

    private bool MatchesRace(ItemTemplate itemTemplate, Race playerRace)
    {
        if (itemTemplate.GetRace() != Race.PC_ALL && itemTemplate.GetRace() != playerRace)
            return false;
        if (race != Race.PC_ALL && race != playerRace)
            return false;
        return true;
    }
}
