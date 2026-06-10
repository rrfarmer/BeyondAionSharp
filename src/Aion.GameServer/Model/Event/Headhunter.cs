using System;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Event;

/// <summary>Java parity: model/event/Headhunter (Estrayl, AION 4.8). implements Comparable+Persistable→IComparable&lt;Headhunter&gt;+IPersistable; PersistentState→IPersistable.PersistentState; currentTimeMillis→DateTimeOffset.UtcNow.ToUnixTimeMilliseconds.</summary>
public class Headhunter : IComparable<Headhunter>, IPersistable
{
    private IPersistable.PersistentState state;
    private readonly int hunterId;
    private int accumulatedKills;
    private long lastUpdate;

    public Headhunter(int hunterId, int accumulatedKills, long lastUpdate, IPersistable.PersistentState state)
    {
        this.hunterId = hunterId;
        this.accumulatedKills = accumulatedKills;
        this.state = state;
        this.lastUpdate = lastUpdate;
    }

    public int GetHunterId()
    {
        return hunterId;
    }

    public int GetKills()
    {
        return accumulatedKills;
    }

    public void SetKills(int accumulatedKills)
    {
        this.accumulatedKills = accumulatedKills;
    }

    public int IncrementAndGetKills()
    {
        accumulatedKills++;
        lastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        state = IPersistable.PersistentState.UPDATE_REQUIRED;
        return accumulatedKills;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return state;
    }

    public void SetPersistentState(IPersistable.PersistentState state)
    {
        this.state = state;
    }

    public long GetLastUpdate()
    {
        return lastUpdate;
    }

    public int CompareTo(Headhunter hunter)
    {
        if (accumulatedKills > hunter.GetKills())
            return -1;
        else if (accumulatedKills < hunter.GetKills())
            return 1;
        else // accumulatedKills == hunter.getKills()
            return lastUpdate > hunter.GetLastUpdate() ? -1 : 1;
    }
}
