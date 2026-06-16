using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Challenge;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ChallengeData (ViAl). @XmlRootElement(challenge_tasks); afterUnmarshal→AfterUnmarshal(object). NOTE: task list not nulled (matches Java).</summary>
[XmlRoot("challenge_tasks")]
public class ChallengeData
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on a protected field).
    [XmlElement("task")] public List<ChallengeTaskTemplate> task;

    [XmlIgnore] protected Dictionary<int, ChallengeTaskTemplate> tasksById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (ChallengeTaskTemplate t in task)
        {
            tasksById[t.GetId()] = t;
        }
    }

    public Dictionary<int, ChallengeTaskTemplate> GetTasks()
    {
        return tasksById;
    }

    public ChallengeTaskTemplate GetTaskByTaskId(int taskId)
    {
        return tasksById.TryGetValue(taskId, out var v) ? v : null;
    }

    public ChallengeTaskTemplate GetTaskByQuestId(int questId)
    {
        foreach (ChallengeTaskTemplate ct in tasksById.Values)
        {
            foreach (ChallengeQuestTemplate cq in ct.GetQuests())
                if (cq.GetId() == questId)
                    return ct;
        }
        return null;
    }

    public ChallengeQuestTemplate GetQuestByQuestId(int questId)
    {
        foreach (ChallengeTaskTemplate ct in tasksById.Values)
        {
            foreach (ChallengeQuestTemplate cq in ct.GetQuests())
                if (cq.GetId() == questId)
                    return cq;
        }
        return null;
    }

    public int Size()
    {
        return tasksById.Count;
    }
}
