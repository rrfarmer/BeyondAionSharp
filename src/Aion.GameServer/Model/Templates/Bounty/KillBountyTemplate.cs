using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Bounty;

/// <summary>Java parity: model/templates/bounty/KillBountyTemplate (Estrayl).</summary>
[XmlType("KillBounty")]
public class KillBountyTemplate
{
    // Public so XmlSerializer can populate them (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("type")] public BountyType type;
    [XmlAttribute("kill_count")] public int killCount;
    [XmlAttribute("is_random_reward")] public bool isRandomReward;
    [XmlAttribute("race")] public Race race = Race.PC_ALL;

    [XmlElement("bounty")] public List<BountyTemplate> bounties;

    public BountyType GetBountyType()
    {
        return type;
    }

    public int GetKillCount()
    {
        return killCount;
    }

    public bool IsRandomReward()
    {
        return isRandomReward;
    }

    public Race GetRaceCondition()
    {
        return race;
    }

    public List<BountyTemplate> GetBounties()
    {
        return bounties;
    }
}
