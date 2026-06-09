using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Questengine;
using Aion.GameServer.Questengine.Handlers.Template;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/KillSpawnedData (extends MonsterHuntData; propOrder monster).</summary>
[XmlType("KillSpawnedData")]
public class KillSpawnedData : MonsterHuntData
{
    [XmlElement("monster")] protected List<Monster> monster;

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new KillSpawned(id, startNpcIds, endNpcIds, monster));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        foreach (Monster m in monster)
        {
            List<int> npcIds = m.GetNpcIds();
            if (npcIds != null && npcIds.Count > 1 && npcIds.Contains(npcId))
                return new HashSet<int>(npcIds.Where(id => id != npcId));
        }
        return base.GetAlternativeNpcs(npcId);
    }
}
