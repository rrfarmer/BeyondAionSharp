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
/// Non-generic base for the team hierarchy. Faithful equivalent of Java's <c>GeneralTeam&lt;?, ?&gt;</c> wildcard usage:
/// C# generic invariance means consumers that hold a <c>GeneralTeam&lt;?, ?&gt;</c> (e.g. WorldMapInstance.registeredTeam,
/// PortalService) cannot reference the closed <c>GeneralTeam&lt;M, TM&gt;</c>, so this carries all of the state and the
/// AionObject-typed surface. The generic <see cref="GeneralTeam{M, TM}"/> derives from it and adds the strongly-typed
/// accessors (housing-web non-generic-base pattern).
/// </summary>
public abstract class GeneralTeam : AionObject
{
    private static readonly ILogger log = NullLogger.Instance;
    protected readonly ConcurrentDictionary<int, ITeamMember<AionObject>> members = new();
    private readonly object teamLock = new object();
    private ITeamMember<AionObject> leader;

    protected GeneralTeam(int objId, bool autoReleaseObjectId)
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

    public bool HasMember(int objectId)
    {
        return members.ContainsKey(objectId);
    }

    public List<AionObject> GetMembers()
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

    public AionObject GetLeaderObject()
    {
        return leader.GetObject();
    }

    public bool IsLeader(AionObject member)
    {
        return leader.GetObject().Equals(member);
    }

    // --- protected leader storage shared with the generic subclass ---

    protected ITeamMember<AionObject> LeaderMember => leader;

    protected void SetLeaderMember(ITeamMember<AionObject> member)
    {
        if (leader != null)
            throw new InvalidOperationException("Leader should be not initialized");
        if (member == null)
            throw new System.NullReferenceException("Leader should not be null");
        this.leader = member;
    }

    protected void ChangeLeaderMember(ITeamMember<AionObject> member)
    {
        if (leader == null)
            throw new System.NullReferenceException("Leader should already be set");
        if (member == null)
            throw new System.NullReferenceException("New leader should not be null");
        if (leader.Equals(member))
            throw new ArgumentException(member + " is already the team leader");
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

    public abstract List<Aion.GameServer.Model.GameObjects.Players.Player> GetOnlineMembers();

    public abstract LootGroupRules GetLootGroupRules();

    public abstract void SendPackets(params Aion.GameServer.Network.Aion.AionServerPacket[] packets);
}

/// <summary>
/// Java parity: model/team/GeneralTeam (ATracer).
/// Java AionServerPacket → AionServerPacket; java.util.function.Predicate/Consumer/Function → System.Predicate/Action/Func.
/// Derives from the non-generic <see cref="GeneralTeam"/> base (faithful equivalent of Java <c>GeneralTeam&lt;?, ?&gt;</c>).
/// </summary>
public abstract class GeneralTeam<M, TM> : GeneralTeam
    where M : AionObject
    where TM : class, ITeamMember<M>
{
    protected GeneralTeam(int objId, bool autoReleaseObjectId)
        : base(objId, autoReleaseObjectId)
    {
    }

    /// <summary>Strongly-typed view over the (non-generic-base) member values for subclasses.</summary>
    protected IEnumerable<TM> MemberValues => members.Values.Cast<TM>();

    public TM GetMember(int objectId)
    {
        return members.TryGetValue(objectId, out ITeamMember<AionObject> m) ? (TM)m : null;
    }

    public virtual void AddMember(TM member)
    {
        if (member == null)
            throw new System.NullReferenceException("Team member should be not null");
        bool existed = members.ContainsKey(member.GetObjectId());
        members[member.GetObjectId()] = member;
        if (existed)
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
        members.TryRemove(objectId, out ITeamMember<AionObject> removed);
        if (removed == null)
            throw new InvalidOperationException("Team member is already removed");
        TM removedMember = (TM)removed;
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
            foreach (ITeamMember<AionObject> member in members.Values)
            {
                consumer((TM)member);
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
            foreach (ITeamMember<AionObject> member in members.Values)
                consumer((M)member.GetObject());
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
            foreach (ITeamMember<AionObject> member in members.Values)
            {
                if (!function((M)member.GetObject()))
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
        return members.Values.Select(m => (TM)m).Where(m => predicate(m)).ToList();
    }

    public List<M> FilterMembers(Predicate<M> predicate)
    {
        return members.Values.Select(m => (M)m.GetObject()).Where(o => predicate(o)).ToList();
    }

    public new List<M> GetMembers()
    {
        return members.Values.Select(m => (M)m.GetObject()).ToList();
    }

    public TM GetLeader()
    {
        return (TM)LeaderMember;
    }

    public new M GetLeaderObject()
    {
        return (M)LeaderMember.GetObject();
    }

    public bool IsLeader(M member)
    {
        return LeaderMember.GetObject().Equals(member);
    }

    public void ChangeLeader(TM member)
    {
        ChangeLeaderMember(member);
    }

    protected void SetLeader(TM member)
    {
        SetLeaderMember(member);
    }

    public abstract void SendPacket(Predicate<M> predicate, params Aion.GameServer.Network.Aion.AionServerPacket[] packets);
}
