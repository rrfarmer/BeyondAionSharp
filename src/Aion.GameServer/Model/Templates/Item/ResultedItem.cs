using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>Java parity: model/templates/item/ResultedItem (antness, Neon).</summary>
[XmlType("ResultedItem")]
public class ResultedItem
{
    [XmlAttribute("id")] private int itemId;
    [XmlAttribute("min_count")] private int minCount = 1;
    [XmlAttribute("max_count")] private int maxCount;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;

    // Java parity: @XmlList @XmlAttribute(name="player_classes") List<PlayerClass> — space-separated.
    private List<PlayerClass> playerClasses;

    [XmlAttribute("player_classes")]
    public string PlayerClassesRaw
    {
        get => playerClasses == null ? null : string.Join(" ", playerClasses);
        set => playerClasses = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => (PlayerClass) Enum.Parse(typeof(PlayerClass), s)).ToList();
    }

    // Java parity: afterUnmarshal(Unmarshaller, Object parent). StaticDataListener (Unmarshaller-keyed) has no C# analog; falls back to DataManager.ITEM_DATA.
    public void AfterUnmarshal(object parent)
    {
        ItemData itemData = DataManager.ITEM_DATA;
        if (itemData.GetItemTemplate(itemId) == null)
            throw new ArgumentException("Decomposable reward item ID is invalid: " + itemId);
        if (minCount <= 0)
            throw new ArgumentException("Decomposable reward item [" + itemId + "] min_count (" + minCount + ") must be greater than 0");
        if (maxCount == 0)
            maxCount = minCount;
        else if (maxCount < minCount)
            throw new ArgumentException(
                "Decomposable reward item [" + itemId + "] max_count (" + maxCount + ") must be unset or greater than min_count (" + minCount + ")");
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetMinCount()
    {
        return minCount;
    }

    public int GetMaxCount()
    {
        return maxCount;
    }

    public Race GetRace()
    {
        return race;
    }

    public List<PlayerClass> GetPlayerClasses()
    {
        return playerClasses;
    }

    public bool IsObtainableFor(Player player)
    {
        return (playerClasses == null || playerClasses.Contains(player.GetPlayerClass()))
            && (race == Race.PC_ALL || race == player.GetRace());
    }
}
