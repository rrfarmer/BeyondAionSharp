using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Portal;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PortalLocData. @XmlRootElement(portal_locs); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("portal_locs")]
public class PortalLocData
{
    [XmlElement("portal_loc")] public List<PortalLoc> portalLoc;

    [XmlIgnore] private readonly Dictionary<int, PortalLoc> portalLocs = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (PortalLoc loc in portalLoc)
        {
            portalLocs[loc.GetLocId()] = loc;
        }
        portalLoc = null;
    }

    public int Size()
    {
        return portalLocs.Count;
    }

    public PortalLoc GetPortalLoc(int locId)
    {
        return portalLocs.TryGetValue(locId, out var v) ? v : null;
    }
}
