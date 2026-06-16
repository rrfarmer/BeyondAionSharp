using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/CraftRecipeGroup.</summary>
[XmlType("CraftRecipeGroup")]
public class CraftRecipeGroup : BonusItemGroup
{
    [XmlElement("item")] public List<CraftRecipe> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<CraftRecipe>();
        return items;
    }
}
