using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Stats;

/// <summary>
/// Java parity: model/templates/stats/AbsoluteStatsTemplate.
/// </summary>
public class AbsoluteStatsTemplate
{
    [XmlElement("modifiers")]
    public ModifiersTemplate modifiers;

    [XmlAttribute("id")]
    public int id;

    public ModifiersTemplate GetModifiers()
    {
        return modifiers;
    }

    public int GetId()
    {
        return id;
    }
}
