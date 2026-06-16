using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/NpcInfos (Hilgert, Pad, Neon). @XmlAttribute List&lt;Integer&gt;→Raw space-sep.</summary>
[XmlType("NpcInfos")]
public class NpcInfos
{
    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("movie")] public int movie;

    public List<int> GetNpcIds()
    {
        return npcIds;
    }

    public int GetMovie()
    {
        return movie;
    }
}
