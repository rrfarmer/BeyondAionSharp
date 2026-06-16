using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Teleport;

/// <summary>Java parity: model/templates/teleport/TeleporterTemplate (orz).</summary>
[XmlRoot("teleporter_template")]
public class TeleporterTemplate
{
    // Java parity: @XmlAttribute(name="npc_ids") List<Integer> — space-separated.
    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("teleportId")] public int teleportId = 0;
    [XmlElement("locations")] public TeleLocIdData teleLocIdData;

    public List<int> GetNpcIds()
    {
        return npcIds;
    }

    public bool ContainNpc(int npcId)
    {
        return npcIds.Contains(npcId);
    }

    public int GetTeleportId()
    {
        return teleportId;
    }

    public TeleLocIdData GetTeleLocIdData()
    {
        return teleLocIdData;
    }
}
