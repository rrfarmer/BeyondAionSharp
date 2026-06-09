using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>Java parity: model/gameobjects/player/QuestStateList.</summary>
public class QuestStateList
{
    private static readonly ILogger log = NullLogger.Instance;
    private readonly SortedDictionary<int, Aion.GameServer.Questengine.Model.QuestState> quests = new SortedDictionary<int, Aion.GameServer.Questengine.Model.QuestState>();
    private readonly HashSet<int> deletedQuests = new HashSet<int>();

    /// <summary>Creates an empty quests list.</summary>
    public QuestStateList()
    {
    }

    /// <summary>True if there is a quest in the list with this id.</summary>
    public bool HasQuest(int questId)
    {
        return quests.ContainsKey(questId);
    }

    public bool AddQuest(int questId, Aion.GameServer.Questengine.Model.QuestState questState)
    {
        lock (this)
        {
            if (quests.ContainsKey(questId))
            {
                log.LogWarning("Tried to add duplicate quest to quest list: " + questId);
                return false;
            }
            quests[questId] = questState;
            return true;
        }
    }

    /// <summary>The quest that was deleted, null if it didn't exist in the list.</summary>
    public Aion.GameServer.Questengine.Model.QuestState DeleteQuest(int questId)
    {
        lock (this)
        {
            if (!quests.Remove(questId, out Aion.GameServer.Questengine.Model.QuestState qs))
                qs = null;
            if (qs != null)
            {
                deletedQuests.Add(qs.GetQuestId());
                qs.SetPersistentState(IPersistable.PersistentState.DELETED);
            }
            return qs;
        }
    }

    public Aion.GameServer.Questengine.Model.QuestState GetQuestState(int questId)
    {
        return quests.TryGetValue(questId, out Aion.GameServer.Questengine.Model.QuestState qs) ? qs : null;
    }

    /// <summary>All quests, including abandoned ones since login.</summary>
    public List<Aion.GameServer.Questengine.Model.QuestState> GetAllQuestState()
    {
        return new List<Aion.GameServer.Questengine.Model.QuestState>(quests.Values);
    }

    /// <summary>All quests that have been completed at least once.</summary>
    public List<Aion.GameServer.Questengine.Model.QuestState> GetCompletedQuests()
    {
        return quests.Values.Where(qs => qs.GetCompleteCount() > 0).ToList();
    }

    /// <summary>All quests that are currently active or locked.</summary>
    public List<Aion.GameServer.Questengine.Model.QuestState> GetUncompletedQuests()
    {
        return quests.Values.Where(qs => qs.GetStatus() != Aion.GameServer.Questengine.Model.QuestStatus.COMPLETE).ToList();
    }

    /// <summary>All normal (light blue) quests that are currently active.</summary>
    public List<Aion.GameServer.Questengine.Model.QuestState> GetNormalQuests()
    {
        List<Aion.GameServer.Questengine.Model.QuestState> questList = new List<Aion.GameServer.Questengine.Model.QuestState>();
        foreach (Aion.GameServer.Questengine.Model.QuestState qs in GetAllQuestState())
        {
            Aion.GameServer.Model.Templates.Quest.QuestCategory qc = Aion.GameServer.Dataholders.DataManager.QUEST_DATA.GetQuestById(qs.GetQuestId()).GetCategory();
            Aion.GameServer.Questengine.Model.QuestStatus s = qs.GetStatus();

            if (qc == Aion.GameServer.Model.Templates.Quest.QuestCategory.QUEST && s != Aion.GameServer.Questengine.Model.QuestStatus.COMPLETE && s != Aion.GameServer.Questengine.Model.QuestStatus.LOCKED)
            {
                questList.Add(qs);
            }
        }
        return questList;
    }

    /// <summary>IDs of all quests specifically marked as deleted (cleared after each DB update).</summary>
    public ISet<int> GetDeletedQuestIds()
    {
        return deletedQuests;
    }
}
