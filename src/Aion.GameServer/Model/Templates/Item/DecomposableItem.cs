using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/DecomposableItem (antness).</summary>
[XmlType("DecomposableItem")]
public class DecomposableItem
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("selectable")] public bool isSelectable;
    [XmlAttribute("only_one")] public bool isOnlyOne;
    [XmlAttribute("override")] public bool isOverride;
    [XmlElement("set")] public List<DecomposableSet> sets;

    public void AfterUnmarshal(object parent)
    {
        sets ??= [];
        // the selection window lists the first applicable set as a whole and grants the picked item, nothing is rolled
        if (isSelectable && sets.Any(set => set.GetChance() != 100 || set.GetItems().Any(reward => reward.GetChance() != 100)))
            throw new ArgumentException("Selectable decomposable item " + itemId + " cannot have chances");
        if (!isSelectable && isOverride && sets.Any(set => set.GetItems().Sum(item => (double)item.GetChance()) >= 100.1))
            throw new ArgumentException("The sum of chances in a set must not exceed 100 % (decomposable item " + itemId + ")");
        if (isOnlyOne) // the rarest branch gets the first roll, so the likelier ones are only reached when the rarer ones miss
            sets = sets.OrderBy(set => set.GetChance()).ToList();
    }

    public int GetItemId()
    {
        return itemId;
    }

    public bool IsSelectable()
    {
        return isSelectable;
    }

    public bool IsOverride()
    {
        return isOverride;
    }

    public List<DecomposableSet> GetSets()
    {
        return sets;
    }

    public DecomposableSet GetSelectableSet(Player player)
    {
        foreach (DecomposableSet set in sets)
        {
            if (set.IsApplicableTo(player) && set.GetItems().Count > 0)
                return set;
        }
        return null;
    }

    /// <summary>
    /// Rolls every applicable branch. Unless the item only ever yields one reward, every branch that fires contributes a reward.
    /// </summary>
    public List<DecomposedItem> Decompose(Player player)
    {
        List<DecomposedItem> items = [];
        foreach (DecomposableSet set in sets)
        {
            if (!set.IsApplicableTo(player) || !set.Roll())
                continue;
            DecomposedItem item = set.SelectItem();
            if (item != null)
                items.Add(item);
            if (isOnlyOne)
                break;
        }
        return items;
    }
}
