using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Bounty;

/// <summary>Java parity: model/templates/bounty/KillBountyTemplate (Estrayl).</summary>
[XmlType("KillBounty")]
public class KillBountyTemplate
{
    [XmlAttribute("type")] private BountyType type;
    [XmlAttribute("kill_count")] private int killCount;
    [XmlAttribute("is_random_reward")] private bool isRandomReward;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;

    [XmlElement("bounty")] private List<BountyTemplate> bounties;

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
