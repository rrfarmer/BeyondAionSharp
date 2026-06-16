using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Portal;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/Portal2Data (xTz). @XmlRootElement(portal_templates2); afterUnmarshal→AfterUnmarshal(object). (Java's PortalService import is javadoc-only.)</summary>
[XmlRoot("portal_templates2")]
public class Portal2Data
{
    [XmlElement("portal_use")] public List<PortalUse> portalUse;
    [XmlElement("portal_dialog")] public List<PortalDialog> portalDialog;
    [XmlElement("portal_scroll")] public List<PortalScroll> portalScroll;

    [XmlIgnore] private readonly Dictionary<int, PortalUse> portalUses = new();
    [XmlIgnore] private readonly Dictionary<int, PortalDialog> portalDialogs = new();
    [XmlIgnore] private readonly Dictionary<string, PortalScroll> portalScrolls = new();

    public void AfterUnmarshal(object parent)
    {
        if (portalUse != null)
        {
            foreach (PortalUse portal in portalUse)
            {
                portalUses[portal.GetNpcId()] = portal;
            }
            portalUse = null;
        }
        if (portalDialog != null)
        {
            foreach (PortalDialog portal in portalDialog)
            {
                portalDialogs[portal.GetNpcId()] = portal;
            }
            portalDialog = null;
        }
        if (portalScroll != null)
        {
            foreach (PortalScroll portal in portalScroll)
            {
                portalScrolls[portal.GetName()] = portal;
            }
            portalScroll = null;
        }
    }

    public int Size()
    {
        return portalScrolls.Count + portalDialogs.Count + portalUses.Count;
    }

    /// <summary>
    /// Tries to find the portal for the player's race, but can return the path of the opposite race if there is no matching one
    /// (so an invalid race error can be sent afterwards).
    /// </summary>
    public PortalPath GetPortalDialogPath(int npcId, int dialogActionId, Player player)
    {
        PortalDialog portal = portalDialogs.TryGetValue(npcId, out var p) ? p : null;
        if (portal != null)
        {
            PortalPath matchingPortalPath = null;
            foreach (PortalPath path in portal.GetPortalPaths())
            {
                if (path.GetDialog() == dialogActionId)
                {
                    if (path.GetRace() == player.GetRace() || path.GetRace() == Race.PC_ALL)
                        return path;
                    matchingPortalPath = path;
                }
            }
            return matchingPortalPath; // return any matched path to send invalid race error afterwards
        }
        return null;
    }

    public PortalPath GetPortalUsePath(int npcId, Player player)
    {
        PortalUse portal = portalUses.TryGetValue(npcId, out var p) ? p : null;
        if (portal != null)
        {
            PortalPath matchingPortalPath = null;
            foreach (PortalPath path in portal.GetPortalPaths())
            {
                if (player.GetRace() == path.GetRace() || path.GetRace() == Race.PC_ALL)
                    return path;
                matchingPortalPath = path;
            }
            return matchingPortalPath; // return any matched path to send invalid race error afterwards
        }
        return null;
    }

    public bool IsPortalNpc(int npcId)
    {
        return (portalUses.TryGetValue(npcId, out _)) || (portalDialogs.TryGetValue(npcId, out _));
    }

    public PortalScroll GetPortalScroll(string name)
    {
        return portalScrolls.TryGetValue(name, out var v) ? v : null;
    }

    public int GetTeleportDialogId(int npcId)
    {
        PortalDialog portal = portalDialogs.TryGetValue(npcId, out var p) ? p : null;
        return portal == null ? 1011 : portal.GetTeleportDialogId();
    }
}
