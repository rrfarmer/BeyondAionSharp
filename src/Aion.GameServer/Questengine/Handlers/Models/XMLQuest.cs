using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Questengine;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/XMLQuest (@XmlType QuestScriptData). @XmlSeeAlso→[XmlInclude] (16 data-holder subclasses red until ported).</summary>
[XmlType("QuestScriptData")]
[XmlInclude(typeof(CraftingRewardsData))]
[XmlInclude(typeof(FountainRewardsData))]
[XmlInclude(typeof(ItemCollectingData))]
[XmlInclude(typeof(ItemOrdersData))]
[XmlInclude(typeof(KillInWorldData))]
[XmlInclude(typeof(KillInZoneData))]
[XmlInclude(typeof(KillSpawnedData))]
[XmlInclude(typeof(MentorMonsterHuntData))]
[XmlInclude(typeof(MonsterHuntData))]
[XmlInclude(typeof(RelicRewardsData))]
[XmlInclude(typeof(ReportOnLevelUpData))]
[XmlInclude(typeof(ReportToData))]
[XmlInclude(typeof(ReportToManyData))]
[XmlInclude(typeof(SkillUseData))]
[XmlInclude(typeof(WorkOrdersData))]
[XmlInclude(typeof(XmlQuestData))]
public abstract class XMLQuest
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("movie")] protected int questMovie;
    [XmlAttribute("mission")] protected bool mission;

    public int GetId()
    {
        return id;
    }

    public abstract void Register(QuestEngine questEngine);

    public abstract ISet<int> GetAlternativeNpcs(int npcId);
}
