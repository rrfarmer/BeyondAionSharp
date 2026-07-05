using System;
using System.Collections.Generic;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Legacy;

namespace Aion.GameServer.Model.Team.Alliance;

/// <summary>Java parity: model/team/alliance/PlayerAlliance (ATracer). extends TemporaryPlayerTeam&lt;PlayerAllianceMember&gt;.</summary>
public class PlayerAlliance : TemporaryPlayerTeam<PlayerAllianceMember>
{
    private readonly Dictionary<int, PlayerAllianceGroup> groups = new Dictionary<int, PlayerAllianceGroup>();
    // Java parity: CopyOnWriteArrayList — the exposed collection is mutated (Assign/Change/Leave
    // vice-captain events) and iterated (ChangeAllianceLeaderEvent, SM_ALLIANCE_INFO) concurrently,
    // so it must be copy-on-write itself (a plain List threw "Collection was modified" mid-iteration).
    private readonly ICollection<int> viceCaptainIds = new Aion.GameServer.Utils.CopyOnWriteArrayList<int>();
    private int allianceReadyStatus;
    private TeamType type;
    private Aion.GameServer.Model.Team.League.League league;

    public PlayerAlliance(PlayerAllianceMember leader, TeamType type)
        : base(Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId(), true)
    {
        this.type = type;
        SetLeader(leader);
        for (int groupId = 1000; groupId <= 1003; groupId++)
        {
            groups[groupId] = new PlayerAllianceGroup(this, groupId);
        }
    }

    public override void AddMember(PlayerAllianceMember member)
    {
        base.AddMember(member);
        PlayerAllianceGroup openAllianceGroup = GetOpenAllianceGroup();
        openAllianceGroup.AddMember(member);
    }

    protected override void OnRemoveMember(PlayerAllianceMember member)
    {
        member.GetPlayerAllianceGroup().RemoveMember(member);
    }

    public override int GetMaxMemberCount()
    {
        return 24;
    }

    public override int GetMinExpPlayerLevel()
    {
        int minLvl = 99;
        foreach (Aion.GameServer.Model.GameObjects.Players.Player member in GetMembers())
        {
            if (member.GetLevel() < minLvl)
            {
                minLvl = member.GetLevel();
            }
        }
        return minLvl;
    }

    public override int GetMaxExpPlayerLevel()
    {
        int maxLvl = 1;
        foreach (Aion.GameServer.Model.GameObjects.Players.Player member in GetMembers())
        {
            if (member.GetLevel() > maxLvl)
            {
                maxLvl = member.GetLevel();
            }
        }
        return maxLvl;
    }

    public PlayerAllianceGroup GetOpenAllianceGroup()
    {
        Lock();
        try
        {
            for (int groupId = 1000; groupId <= 1003; groupId++)
            {
                PlayerAllianceGroup playerAllianceGroup = groups[groupId];
                if (!playerAllianceGroup.IsFull())
                {
                    return playerAllianceGroup;
                }
            }
        }
        finally
        {
            Unlock();
        }
        throw new InvalidOperationException("All alliance groups are full.");
    }

    public PlayerAllianceGroup GetAllianceGroup(int allianceGroupId)
    {
        PlayerAllianceGroup allianceGroup = groups.TryGetValue(allianceGroupId, out PlayerAllianceGroup g) ? g : null;
        if (allianceGroup == null)
            throw new System.NullReferenceException("No such alliance group " + allianceGroupId);
        return allianceGroup;
    }

    public ICollection<int> GetViceCaptainIds()
    {
        return viceCaptainIds;
    }

    public bool IsViceCaptain(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return viceCaptainIds.Contains(player.GetObjectId());
    }

    public bool IsSomeCaptain(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return IsLeader(player) || IsViceCaptain(player);
    }

    public int GetAllianceReadyStatus()
    {
        return allianceReadyStatus;
    }

    public void SetAllianceReadyStatus(int allianceReadyStatus)
    {
        this.allianceReadyStatus = allianceReadyStatus;
    }

    public Aion.GameServer.Model.Team.League.League GetLeague()
    {
        return league;
    }

    public void SetLeague(Aion.GameServer.Model.Team.League.League league)
    {
        this.league = league;
    }

    public bool IsInLeague()
    {
        return this.league != null;
    }

    public int GroupSize()
    {
        return groups.Count;
    }

    public ICollection<PlayerAllianceGroup> GetGroups()
    {
        return groups.Values;
    }

    public TeamType GetTeamType()
    {
        return type;
    }

    public override LootGroupRules GetLootGroupRules()
    {
        return league == null ? base.GetLootGroupRules() : league.GetLootGroupRules();
    }
}
