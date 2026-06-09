using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Bounty;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/KillBountyData. @XmlRootElement(kill_bounties).</summary>
[XmlRoot("kill_bounties")]
public class KillBountyData
{
    [XmlElement("kill_bounty")] private List<KillBountyTemplate> killBounties;

    public int Size()
    {
        return killBounties.Count;
    }

    public List<KillBountyTemplate> GetKillBounties()
    {
        return killBounties;
    }
}
