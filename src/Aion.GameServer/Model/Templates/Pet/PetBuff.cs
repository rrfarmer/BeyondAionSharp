using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetBuff (Rolandas).</summary>
[XmlType("PetBuff")]
public class PetBuff
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("feed_count")] protected int feedCount;
    [XmlElement("modifiers")] protected List<ModifiersTemplate> modifiers;

    public int GetId()
    {
        return id;
    }

    public int GetFeedCount()
    {
        return feedCount;
    }

    public List<ModifiersTemplate> GetModifiers()
    {
        if (modifiers == null)
            modifiers = new List<ModifiersTemplate>();
        return modifiers;
    }
}
