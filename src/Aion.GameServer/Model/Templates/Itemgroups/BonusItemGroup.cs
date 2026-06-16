using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/BonusItemGroup. implements Chance→IChance; @XmlSeeAlso→[XmlInclude]; List&lt;? extends ItemRaceEntry&gt;→IReadOnlyList&lt;ItemRaceEntry&gt;.</summary>
[XmlType("BonusItemGroup")]
[XmlInclude(typeof(CraftItemGroup))]
[XmlInclude(typeof(CraftRecipeGroup))]
[XmlInclude(typeof(EventGroup))]
[XmlInclude(typeof(ManastoneGroup))]
[XmlInclude(typeof(FoodGroup))]
[XmlInclude(typeof(MedicineGroup))]
[XmlInclude(typeof(OreGroup))]
[XmlInclude(typeof(GatherGroup))]
[XmlInclude(typeof(EnchantGroup))]
[XmlInclude(typeof(BossGroup))]
public abstract class BonusItemGroup : IChance
{
    [XmlAttribute("bonusType")] public BonusType bonusType;
    [XmlAttribute("chance")] public float chance = 100f;

    public BonusType GetBonusType()
    {
        return bonusType;
    }

    public float GetChance()
    {
        return chance;
    }

    public abstract IReadOnlyList<ItemRaceEntry> GetItems();
}
