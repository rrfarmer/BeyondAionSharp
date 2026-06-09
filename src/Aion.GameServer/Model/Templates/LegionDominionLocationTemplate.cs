using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/LegionDominionLocationTemplate (Yeats, Sykra).</summary>
[XmlType("LegionDominionLocation")]
public class LegionDominionLocationTemplate : IL10n
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("world_id")] protected int worldId;
    [XmlAttribute("race")] protected Race race;
    [XmlAttribute("zone")] protected string zone;
    [XmlAttribute("name_id")] protected int nameId;
    [XmlElement("reward")] protected List<LegionDominionReward> reward;
    [XmlElement("invasion_rift")] protected LegionDominionInvasionRift invasionRift;

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
