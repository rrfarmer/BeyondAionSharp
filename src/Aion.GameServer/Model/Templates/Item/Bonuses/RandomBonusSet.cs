using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Items.Bonuses;

/// <summary>Java parity: model/templates/item/bonuses/RandomBonusSet.</summary>
[XmlType("RandomBonusSet")]
public class RandomBonusSet
{
    [XmlElement("modifiers")] public List<ModifiersTemplate> modifiers;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("type")] public StatBonusType bonusType;

    public List<ModifiersTemplate> GetModifiers()
    {
        return modifiers;
    }

    public int GetId()
    {
        return id;
    }

    public StatBonusType GetBonusType()
    {
        return bonusType;
    }
}
