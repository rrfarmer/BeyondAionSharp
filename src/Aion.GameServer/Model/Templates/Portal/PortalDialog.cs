using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalDialog (xTz).</summary>
[XmlType("PortalDialog")]
public class PortalDialog
{
    [XmlElement("portal_path")] public List<PortalPath> portalPaths;
    [XmlAttribute("npc_id")] public int npcId;
    [XmlAttribute("teleport_dialog_id")] public int teleportDialogId = 1011;

    public List<PortalPath> GetPortalPaths()
    {
        return portalPaths;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int GetTeleportDialogId()
    {
        return teleportDialogId;
    }
}
