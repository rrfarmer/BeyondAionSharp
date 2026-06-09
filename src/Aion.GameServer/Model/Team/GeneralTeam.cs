using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Team.Common.Legacy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Team;

/// <summary>
/// Java parity: model/team/GeneralTeam (ATracer).
/// Java AionServerPacket → GameServerPacket; java.util.function.Predicate/Consumer/Function → System.Predicate/Action/Func.
/// </summary>
public abstract class GeneralTeam<M, TM> : AionObject
    where M : AionObject
    where TM : class, ITeamMember<M>
{
    private static readonly ILogger log = NullLogger.Instance;
    protected readonly ConcurrentDictionary<int, TM> members = new();
    private readonly object teamLock = new object();
    private TM leader;

    public GeneralTeam(int objId, bool autoReleaseObjectId)
        : base(objId, autoReleaseObjectId)
    {
    }

    public void OnEvent(ITeamEvent @event)
    {
        Lock();
        try
        {
            if (@event.CheckCondition())
            {
                @event.HandleEvent();
            }
            else
            {
                log.LogWarning("[TEAM] skipped event: " + @event + " group: " + this);
            }
        }
        finally
        {
            Unlock();
        }
    }

    public TM GetMember(int objectId)
    {
        return members.TryGetValue(objectId, out TM m) ? m : null;
    }

    public bool HasMember(int objectId)
    {
        return GetMember(objectId) != null;
    }

    public virtual void AddMember(TM member)
    {
        if (member == null)
            throw new System.NullReferenceException("Team member should be not null");
        members.TryGetValue(member.GetObjectId(), out TM prev);
        members[member.GetObjectId()] = member;
        if (prev != null)
            throw new InvalidOperationException("Team member is already added");
    }

    public TM RemoveMember(TM member)
    {
        if (member == null)
            throw new System.NullReferenceException("Team member should be not null");
        return RemoveMember(member.GetObjectId());
    }

    public TM RemoveMember(int objectId)
    {
        members.TryRemove(objectId, out TM removedMember);
        if (removedMember == null)
            throw new InvalidOperationException("Team member is already removed");
        OnRemoveMember(removedMember);
        return removedMember;
    }

    protected abstract void OnRemoveMember(TM member);

    /// <summary>Apply some function on all team members (state changes only).</summary>
    public void ForEachTeamMember(Action<TM> consumer)
    {
        Lock();
        try
        {
            foreach (TM member in members.Values)
            {
                consumer(member);
            }
        }
        finally
        {
            Unlock();
        }
    }

    /// <summary>Apply some function on all team member's objects (state changes only).</summary>
    public void ForEach(Action<M> consumer)
    {
        Lock();
        try
        {
            foreach (TM member in members.Values)
                consumer(member.GetObject());
        }
        finally
        {
            Unlock();
        }
    }

    /// <summary>Apply some function on all team member's objects until it returns false (state changes only).</summary>
    public void ApplyOnMembers(Func<M, bool> function)
    {
        Lock();
        try
        {
            foreach (TM member in members.Values)
            {
                if (!function(member.GetObject()))
                {
                    return;
                }
            }
        }
        finally
        {
            Unlock();
        }
    }

    public List<TM> Filter(Predicate<TM> predicate)
    {
        return members.Values.Where(m => predicate(m)).ToList();
    }

    public List<M> FilterMembers(Predicate<M> predicate)
    {
        return members.Values.Select(m => m.GetObject()).Where(o => predicate(o)).ToList();
    }

    public List<M> GetMembers()
    {
        return members.Values.Select(m => m.GetObject()).ToList();
    }

    public int Size()
    {
        return members.Count;
    }

    public bool IsDisbanded()
    {
        return Size() == 0;
    }

    public bool ShouldDisband()
    {
        return Size() == 1; // teams always contain at least two members
    }

    public bool IsFull()
    {
        return Size() == GetMaxMemberCount();
    }

    public int GetTeamId()
    {
        return GetObjectId();
    }

    public override string Name => "Leader: " + leader.GetObject();

    public TM GetLeader()
    {
        return leader;
    }

    public M GetLeaderObject()
    {
        return leader.GetObject();
    }

    public bool IsLeader(M member)
    {
        return leader.GetObject().Equals(member);
    }

    public void ChangeLeader(TM member)
    {
        if (leader == null)
            throw new System.NullReferenceException("Leader should already be set");
        if (member == null)
            throw new System.NullReferenceException("New leader should not be null");
        if (leader.Equals(member))
            throw new ArgumentException(member + " is already the team leader");
        this.leader = member;
    }

    protected void SetLeader(TM member)
    {
        if (leader != null)
            throw new InvalidOperationException("Leader should be not initialized");
        if (member == null)
            throw new System.NullReferenceException("Leader should not be null");
        this.leader = member;
    }

    protected void Lock()
    {
        Monitor.Enter(teamLock);
    }

    protected void Unlock()
    {
        Monitor.Exit(teamLock);
    }

    public abstract Race GetRace();

    public abstract int GetMaxMemberCount();

    public abstract List<Aion.GameServer.Model.GameObjects.Player.Player> GetOnlineMembers();

    public abstract LootGroupRules GetLootGroupRules();

    public abstract void SendPackets(params Aion.GameServer.Network.Aion.GameServerPacket[] packets);

    public abstract void SendPacket(Predicate<M> predicate, params Aion.GameServer.Network.Aion.GameServerPacket[] packets);
}
