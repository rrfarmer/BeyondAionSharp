using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team;

/// <summary>
/// Non-generic base for player teams. Faithful equivalent of Java's wildcard <c>TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt;</c>:
/// consumers (Player.GetCurrentTeam, PlayerRestrictions, event/service layer, etc.) hold this raw type so that PlayerGroup /
/// PlayerAlliance / PlayerAllianceGroup are all assignable to a common type despite C# generic invariance. Member object type is
/// always Player here, so the T-dependent surface is exposed as Player-typed (housing-web non-generic-base pattern).
/// </summary>
public abstract class TemporaryPlayerTeam : GeneralTeam
{
    private LootGroupRules lootGroupRules = new LootGroupRules();
    protected readonly ConcurrentDictionary<int, int> targetIdsByBrandId = new();

    protected TemporaryPlayerTeam(int objId, bool autoReleaseObjectId)
        : base(objId, autoReleaseObjectId)
    {
    }

    /// <summary>Level of the player with lowest exp.</summary>
    public abstract int GetMinExpPlayerLevel();

    /// <summary>Level of the player with highest exp.</summary>
    public abstract int GetMaxExpPlayerLevel();

    public new List<Aion.GameServer.Model.GameObjects.Players.Player> GetMembers()
    {
        return members.Values.Select(m => (Aion.GameServer.Model.GameObjects.Players.Player)m.GetObject()).ToList();
    }

    public ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player> GetMember(int objectId)
    {
        return members.TryGetValue(objectId, out ITeamMember<Aion.GameServer.Model.GameObjects.AionObject> m)
            ? (ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>)m
            : null;
    }

    public new Aion.GameServer.Model.GameObjects.Players.Player GetLeaderObject()
    {
        return (Aion.GameServer.Model.GameObjects.Players.Player)LeaderMember.GetObject();
    }

    public bool IsLeader(Aion.GameServer.Model.GameObjects.Players.Player member)
    {
        return LeaderMember.GetObject().Equals(member);
    }

    /// <summary>Apply some function on all team member's players (state changes only).</summary>
    public void ForEach(Action<Aion.GameServer.Model.GameObjects.Players.Player> consumer)
    {
        Lock();
        try
        {
            foreach (ITeamMember<Aion.GameServer.Model.GameObjects.AionObject> member in members.Values)
                consumer((Aion.GameServer.Model.GameObjects.Players.Player)member.GetObject());
        }
        finally
        {
            Unlock();
        }
    }

    public List<Aion.GameServer.Model.GameObjects.Players.Player> FilterMembers(Predicate<Aion.GameServer.Model.GameObjects.Players.Player> predicate)
    {
        return members.Values.Select(m => (Aion.GameServer.Model.GameObjects.Players.Player)m.GetObject()).Where(o => predicate(o)).ToList();
    }

    /// <summary>Apply some function on all team member's players until it returns false (state changes only).</summary>
    public void ApplyOnMembers(Func<Aion.GameServer.Model.GameObjects.Players.Player, bool> function)
    {
        Lock();
        try
        {
            foreach (ITeamMember<Aion.GameServer.Model.GameObjects.AionObject> member in members.Values)
            {
                if (!function((Aion.GameServer.Model.GameObjects.Players.Player)member.GetObject()))
                    return;
            }
        }
        finally
        {
            Unlock();
        }
    }

    public void UpdateBrand(int brandId, int targetObjectId)
    {
        targetIdsByBrandId[brandId] = targetObjectId;
        SendPackets(new Aion.GameServer.Network.Aion.ServerPackets.SM_SHOW_BRAND(brandId, targetObjectId));
    }

    public void SendBrands(Aion.GameServer.Model.GameObjects.Players.Player member)
    {
        PacketSendUtility.SendPacket(member, new Aion.GameServer.Network.Aion.ServerPackets.SM_SHOW_BRAND(targetIdsByBrandId));
    }

    public override Race GetRace()
    {
        return GetLeaderObject().GetRace();
    }

    public override void SendPackets(params Aion.GameServer.Network.Aion.AionServerPacket[] packets)
    {
        SendPacket(Predicates.AlwaysTrue<Aion.GameServer.Model.GameObjects.Players.Player>(), packets);
    }

    public virtual void SendPacket(Predicate<Aion.GameServer.Model.GameObjects.Players.Player> predicate, params Aion.GameServer.Network.Aion.AionServerPacket[] packets)
    {
        ForEach(player =>
        {
            if (predicate(player))
            {
                foreach (Aion.GameServer.Network.Aion.AionServerPacket packet in packets)
                    PacketSendUtility.SendPacket(player, packet);
            }
        });
    }

    public override List<Aion.GameServer.Model.GameObjects.Players.Player> GetOnlineMembers()
    {
        return GetMembers().Where(p => Predicates.Players.ONLINE(p)).ToList();
    }

    public override LootGroupRules GetLootGroupRules()
    {
        return lootGroupRules;
    }

    public void SetLootGroupRules(LootGroupRules lootGroupRules)
    {
        this.lootGroupRules = lootGroupRules;
        if (lootGroupRules != null && lootGroupRules.GetLootRule() == LootRuleType.FREEFORALL)
            SendPacket(Predicates.Players.WITH_LOOT_PET, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_LOOTING_PET_MESSAGE03());
    }
}

/// <summary>Java parity: model/team/TemporaryPlayerTeam (ATracer). extends GeneralTeam&lt;Player, TM&gt;.</summary>
public abstract class TemporaryPlayerTeam<TM> : TemporaryPlayerTeam
    where TM : class, ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>
{
    protected TemporaryPlayerTeam(int objId, bool autoReleaseObjectId)
        : base(objId, autoReleaseObjectId)
    {
    }

    // --- strongly-typed (TM) views, paralleling the generic GeneralTeam<M, TM> surface ---

    public new TM GetMember(int objectId)
    {
        return members.TryGetValue(objectId, out ITeamMember<Aion.GameServer.Model.GameObjects.AionObject> m) ? (TM)m : null;
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
        members.TryRemove(objectId, out ITeamMember<Aion.GameServer.Model.GameObjects.AionObject> removed);
        if (removed == null)
            throw new InvalidOperationException("Team member is already removed");
        TM removedMember = (TM)removed;
        OnRemoveMember(removedMember);
        return removedMember;
    }

    protected abstract void OnRemoveMember(TM member);

    public void ForEachTeamMember(Action<TM> consumer)
    {
        Lock();
        try
        {
            foreach (ITeamMember<Aion.GameServer.Model.GameObjects.AionObject> member in members.Values)
                consumer((TM)member);
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

    public TM GetLeader()
    {
        return (TM)LeaderMember;
    }

    public void ChangeLeader(TM member)
    {
        ChangeLeaderMember(member);
    }

    protected void SetLeader(TM member)
    {
        SetLeaderMember(member);
    }
}
