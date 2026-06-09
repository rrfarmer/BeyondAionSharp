using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Player.Npcfaction;

/// <summary>Java parity: model/gameobjects/player/npcFaction/NpcFaction implements Persistable.</summary>
public class NpcFaction : IPersistable
{
    private int id;
    private int time;
    private bool active;
    private bool mentor;
    private ENpcFactionQuestState state;
    private int questId;
    private IPersistable.PersistentState persistentState;

    public NpcFaction(int id, int time, bool active, ENpcFactionQuestState state, int questId)
    {
        this.id = id;
        this.time = time;
        this.active = active;
        this.state = state;
        this.mentor = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionById(id).IsMentor();
        this.questId = questId;
        this.persistentState = IPersistable.PersistentState.NEW;
    }

    /// <summary>the id</summary>
    public int GetId()
    {
        return id;
    }

    /// <summary>the time</summary>
    public int GetTime()
    {
        return time;
    }

    /// <summary>the active</summary>
    public bool IsActive()
    {
        return active;
    }

    /// <summary>the mentor</summary>
    public bool IsMentor()
    {
        return mentor;
    }

    /// <summary>the state</summary>
    public ENpcFactionQuestState GetState()
    {
        return state;
    }

    /// <param name="time">the time to set</param>
    public void SetTime(int time)
    {
        this.time = time;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <param name="active">the active to set</param>
    public void SetActive(bool active)
    {
        this.active = active;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <param name="state">the state to set</param>
    public void SetState(ENpcFactionQuestState state)
    {
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        this.state = state;
    }

    /// <summary>the questId</summary>
    public int GetQuestId()
    {
        return questId;
    }

    /// <param name="questId">the questId to set</param>
    public void SetQuestId(int questId)
    {
        this.questId = questId;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>the persistentState</summary>
    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    /// <param name="persistentState">the persistentState to set</param>
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.NOACTION;
                else
                    this.persistentState = IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState != IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
                break;
            case IPersistable.PersistentState.NOACTION:
                break;
            default:
                this.persistentState = persistentState;
                break;
        }
    }
}
