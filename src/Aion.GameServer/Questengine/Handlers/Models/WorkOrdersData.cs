using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Questengine;
using Aion.GameServer.Questengine.Handlers.Template;

namespace Aion.GameServer.Questengine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/WorkOrdersData (propOrder giveComponents).</summary>
[XmlType("WorkOrdersData")]
public class WorkOrdersData : XMLQuest
{
    [XmlElement("give_component")] protected List<QuestItems> giveComponents;

    protected List<int> startNpcIds;

    [XmlAttribute("start_npc_ids")]
    public string StartNpcIdsRaw
    {
        get => startNpcIds == null ? null : string.Join(" ", startNpcIds);
        set => startNpcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("recipe_id")] protected int recipeId;

    public int GetRecipeId()
    {
        return recipeId;
    }

    public override void Register(QuestEngine questEngine)
    {
        questEngine.AddQuestHandler(new WorkOrders(id, startNpcIds, giveComponents, recipeId));
    }

    public override ISet<int> GetAlternativeNpcs(int npcId)
    {
        if (startNpcIds != null && startNpcIds.Count > 1 && startNpcIds.Contains(npcId))
            return new HashSet<int>(startNpcIds.Where(id => id != npcId));
        return null;
    }
}
