using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Challenge;

namespace Aion.GameServer.Model.Challenge;

/// <summary>Java parity: model/challenge/ChallengeQuest (ViAl). implements Persistable→IPersistable; synchronized→lock(this); PersistentState→IPersistable.PersistentState. ChallengeQuestTemplate red-tolerated.</summary>
public class ChallengeQuest : IPersistable
{
    private readonly ChallengeQuestTemplate template;
    private int completeCount;
    private IPersistable.PersistentState persistentState;

    public ChallengeQuest(ChallengeQuestTemplate template, int completeCount)
    {
        this.template = template;
        this.completeCount = completeCount;
    }

    public int GetQuestId()
    {
        return template.GetId();
    }

    public ChallengeQuestTemplate GetQuestTemplate()
    {
        return template;
    }

    public int GetMaxRepeats()
    {
        return template.GetRepeatCount();
    }

    public int GetScorePerQuest()
    {
        return template.GetScore();
    }

    public int GetCompleteCount()
    {
        return completeCount;
    }

    public void IncreaseCompleteCount()
    {
        lock (this)
        {
            this.completeCount++;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        if (this.persistentState == IPersistable.PersistentState.NEW && persistentState == IPersistable.PersistentState.UPDATE_REQUIRED)
            return;
        this.persistentState = persistentState;
    }
}
