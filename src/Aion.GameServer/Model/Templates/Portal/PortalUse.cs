using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalUse (xTz).</summary>
[XmlType("PortalUse")]
public class PortalUse
{
    [XmlElement("portal_path")] private List<PortalPath> portalPaths;
    [XmlAttribute("npc_id")] private int npcId;

    public List<PortalPath> GetPortalPaths()
    {
        return portalPaths;
    }

    public int GetNpcId()
    {
        return npcId;
    }
}
