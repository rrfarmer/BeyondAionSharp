using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Bounty;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/KillBountyData. @XmlRootElement(kill_bounties).</summary>
[XmlRoot("kill_bounties")]
public class KillBountyData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("kill_bounty")] public List<KillBountyTemplate> killBounties;

    public int Size()
    {
        return killBounties.Count;
    }

    public List<KillBountyTemplate> GetKillBounties()
    {
        return killBounties;
    }
}
