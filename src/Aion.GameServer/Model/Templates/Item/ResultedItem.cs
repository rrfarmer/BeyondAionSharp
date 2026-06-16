using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/ResultedItem (antness, Neon).</summary>
[XmlType("ResultedItem")]
public class ResultedItem
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlAttribute("id")] public int itemId;
    [XmlAttribute("min_count")] public int minCount = 1;
    [XmlAttribute("max_count")] public int maxCount;
    [XmlAttribute("race")] public Race race = Race.PC_ALL;

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
        AfterUnmarshal(DataManager.ITEM_DATA, parent);
    }

    // Boot-time overload: during LoadLeafHoldersFromFiles the DataManager singleton bridge is not yet registered,
    // so the cascading parent (DecomposableItemsData.AfterUnmarshal) passes the in-progress ItemData explicitly —
    // mirroring Java's StaticDataListener handing the callback the StaticData currently being unmarshalled.
    public void AfterUnmarshal(ItemData itemData, object parent)
    {
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
