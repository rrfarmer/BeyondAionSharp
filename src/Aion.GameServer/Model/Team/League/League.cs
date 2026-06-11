using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team.League;

/// <summary>Java parity: model/team/league/League (ATracer). extends GeneralTeam&lt;PlayerAlliance, LeagueMember&gt;.</summary>
public class League : GeneralTeam<PlayerAlliance, LeagueMember>
{
    private LootGroupRules lootGroupRules = new LootGroupRules();

    public League(LeagueMember leader)
        : base(Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId(), true)
    {
        SetLeader(leader);
    }

    public override List<Aion.GameServer.Model.GameObjects.Players.Player> GetOnlineMembers()
    {
        return GetMembers().SelectMany(alliance => alliance.GetOnlineMembers()).ToList();
    }

    public override void AddMember(LeagueMember member)
    {
        base.AddMember(member);
        member.GetObject().SetLeague(this);
    }

    protected override void OnRemoveMember(LeagueMember member)
    {
        member.GetObject().SetLeague(null);
    }

    public override int GetMaxMemberCount()
    {
        return 8;
    }

    public override void SendPackets(params Aion.GameServer.Network.Aion.GameServerPacket[] packets)
    {
        foreach (PlayerAlliance alliance in GetMembers())
        {
            alliance.SendPackets(packets);
        }
    }

    public override void SendPacket(Predicate<PlayerAlliance> predicate, params Aion.GameServer.Network.Aion.GameServerPacket[] packets)
    {
        foreach (PlayerAlliance alliance in GetMembers())
        {
            if (predicate(alliance))
                alliance.SendPackets(packets);
        }
    }

    public override Race GetRace()
    {
        return GetLeaderObject().GetRace();
    }

    public Aion.GameServer.Model.GameObjects.Players.Player GetCaptain()
    {
        return GetLeaderObject().GetLeaderObject();
    }

    public override LootGroupRules GetLootGroupRules()
    {
        return lootGroupRules;
    }

    public void SetLootGroupRules(LootGroupRules lootGroupRules)
    {
        this.lootGroupRules = lootGroupRules;
    }

    /// <returns>sorted alliances by position</returns>
    public ICollection<LeagueMember> GetSortedMembers()
    {
        return members.Values.OrderBy(m => m.GetLeaguePosition()).ToList();
    }

    /// <summary>Reorganize alliances positions in league from 0 to size.</summary>
    /// <returns>new league leader</returns>
    public Aion.GameServer.Model.GameObjects.Players.Player Reorganize()
    {
        int position = 0;
        Aion.GameServer.Model.GameObjects.Players.Player newLeader = null;
        foreach (LeagueMember alliance in GetSortedMembers())
        {
            if (alliance.GetLeaguePosition() > position)
            {
                if (position == 0)
                {
                    newLeader = alliance.GetObject().GetLeaderObject();
                    ChangeLeader(alliance);
                }
                alliance.SetLeaguePosition(position);
            }
            position++;
        }
        return newLeader;
    }

    /// <summary>Search for player member in all alliances.</summary>
    /// <returns>player object</returns>
    public Aion.GameServer.Model.GameObjects.Players.Player GetPlayerMember(int playerObjId)
    {
        foreach (PlayerAlliance member in GetMembers())
        {
            PlayerAllianceMember playerMember = member.GetMember(playerObjId);
            if (playerMember != null)
            {
                return playerMember.GetObject();
            }
        }
        return null;
    }

    public void Broadcast()
    {
        Broadcast((PlayerAlliance)null, (Aion.GameServer.Model.GameObjects.Players.Player)null);
    }

    public void Broadcast(Aion.GameServer.Model.GameObjects.Players.Player skippedPlayer)
    {
        Broadcast(null, skippedPlayer);
    }

    public void Broadcast(PlayerAlliance skippedAlliance)
    {
        Broadcast(skippedAlliance, null);
    }

    public void Broadcast(PlayerAlliance skippedAlliance, Aion.GameServer.Model.GameObjects.Players.Player skippedPlayer)
    {
        Lock();
        try
        {
            foreach (LeagueMember memberAlliance in members.Values)
            {
                PlayerAlliance targetAlliance = memberAlliance.GetObject();
                if (!targetAlliance.Equals(skippedAlliance))
                {
                    Predicate<Aion.GameServer.Model.GameObjects.Players.Player> predicate = Predicates.AlwaysTrue<Aion.GameServer.Model.GameObjects.Players.Player>();
                    if (skippedPlayer != null)
                    {
                        predicate = Predicates.Players.AllExcept(skippedPlayer);
                    }
                    targetAlliance.SendPacket(predicate, new Aion.GameServer.Network.Aion.ServerPackets.SM_ALLIANCE_INFO(targetAlliance, skippedAlliance));
                }
            }
        }
        finally
        {
            Unlock();
        }
    }

    public ICollection<Aion.GameServer.Model.GameObjects.Players.Player> GetCaptains()
    {
        List<Aion.GameServer.Model.GameObjects.Players.Player> captains = new List<Aion.GameServer.Model.GameObjects.Players.Player>();
        foreach (LeagueMember member in GetSortedMembers())
        {
            Aion.GameServer.Model.GameObjects.Players.Player leader = member.GetObject().GetLeaderObject();
            if (!captains.Contains(leader))
            {
                captains.Add(leader);
            }
        }
        return captains;
    }
}
