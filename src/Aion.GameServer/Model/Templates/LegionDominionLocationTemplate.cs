using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/LegionDominionLocationTemplate (Yeats, Sykra).</summary>
[XmlType("LegionDominionLocation")]
public class LegionDominionLocationTemplate : IL10n
{
    // Public so XmlSerializer can populate them (JAXB used protected fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("id")] public int id;
    [XmlAttribute("world_id")] public int worldId;
    [XmlAttribute("race")] public Race race;
    [XmlAttribute("zone")] public string zone;
    [XmlAttribute("name_id")] public int nameId;
    [XmlElement("reward")] public List<LegionDominionReward> reward;
    [XmlElement("invasion_rift")] public LegionDominionInvasionRift invasionRift;

    public int GetId()
    {
        return id;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public Race GetRace()
    {
        return race;
    }

    public string GetZone()
    {
        return zone;
    }

    public List<LegionDominionReward> GetRewards()
    {
        return reward;
    }

    public LegionDominionInvasionRift GetInvasionRift()
    {
        return invasionRift;
    }

    public int GetL10nId()
    {
        return nameId;
    }
}
