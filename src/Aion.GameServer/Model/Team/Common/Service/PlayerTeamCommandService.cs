using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Alliance.Events;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;

namespace Aion.GameServer.Model.Team.Common.Service;

/// <summary>Java parity: model/team/common/service/PlayerTeamCommandService (ATracer). switch on TeamCommand; TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt;→TemporaryPlayerTeam&lt;TeamMember&lt;Player&gt;&gt; (matches Player.GetCurrentTeam C# bound); instanceof→is-pattern; Objects.requireNonNull(x,supplier)→null-check+ArgumentNullException. PlayerGroupService/PlayerAllianceService/LeagueService/AssignType red-tolerated.</summary>
public class PlayerTeamCommandService
{
    public static void ExecuteCommand(Player player, TeamCommand command, int memberObjId)
    {
        TemporaryPlayerTeam<ITeamMember<Player>> team = player.GetCurrentTeam();
        if (team == null) // team might have been disbanded or player can have been kicked out of his team by the time the packet arrived
            return;
        switch (command)
        {
            case TeamCommand.GROUP_BAN_MEMBER:
                PlayerGroupService.BanPlayer(FindMember(team, player, memberObjId), player);
                break;
            case TeamCommand.GROUP_SET_LEADER:
                PlayerGroupService.ChangeLeader(FindMember(team, player, memberObjId));
                break;
            case TeamCommand.GROUP_REMOVE_MEMBER:
                PlayerGroupService.RemovePlayer(FindMember(team, player, memberObjId));
                break;
            case TeamCommand.GROUP_START_MENTORING:
                PlayerGroupService.StartMentoring(player);
                break;
            case TeamCommand.GROUP_END_MENTORING:
                PlayerGroupService.StopMentoring(player);
                break;
            case TeamCommand.ALLIANCE_LEAVE:
                PlayerAllianceService.RemovePlayer(player);
                break;
            case TeamCommand.ALLIANCE_BAN_MEMBER:
                PlayerAllianceService.BanPlayer(FindMember(team, player, memberObjId), player);
                break;
            case TeamCommand.ALLIANCE_SET_CAPTAIN:
                PlayerAllianceService.ChangeLeader(FindMember(team, player, memberObjId));
                break;
            case TeamCommand.ALLIANCE_CHECKREADY_CANCEL:
            case TeamCommand.ALLIANCE_CHECKREADY_START:
            case TeamCommand.ALLIANCE_CHECKREADY_AUTOCANCEL:
            case TeamCommand.ALLIANCE_CHECKREADY_NOTREADY:
            case TeamCommand.ALLIANCE_CHECKREADY_READY:
                PlayerAllianceService.CheckReady(player, command);
                break;
            case TeamCommand.ALLIANCE_SET_VICECAPTAIN:
                PlayerAllianceService.ChangeViceCaptain(FindMember(team, player, memberObjId), AssignViceCaptainEvent.AssignType.PROMOTE);
                break;
            case TeamCommand.ALLIANCE_UNSET_VICECAPTAIN:
                PlayerAllianceService.ChangeViceCaptain(FindMember(team, player, memberObjId), AssignViceCaptainEvent.AssignType.DEMOTE);
                break;
            case TeamCommand.LEAGUE_LEAVE:
                LeagueService.RemoveAlliance(player.GetPlayerAlliance());
                break;
            case TeamCommand.LEAGUE_EXPEL:
                LeagueService.ExpelAlliance(FindLeagueAlliance(team, player, memberObjId), player);
                break;
            case TeamCommand.LEAGUE_SET_LEADER:
                PlayerAlliance leagueAlliance = FindLeagueAlliance(team, player, memberObjId).GetObject();
                LeagueService.SetLeader(player, leagueAlliance.GetLeaderObject());
                break;
        }
    }

    private static LeagueMember FindLeagueAlliance(TemporaryPlayerTeam<ITeamMember<Player>> team, Player player, int leagueAllianceId)
    {
        League league = team is PlayerAlliance pa ? pa.GetLeague() : null;
        if (league == null)
            throw new ArgumentNullException(null, player + " tried to execute league command without an active league alliance");
        LeagueMember member = league.GetMember(leagueAllianceId);
        if (member == null)
            throw new ArgumentNullException(null, player + " tried to execute league command on invalid alliance " + leagueAllianceId);
        return member;
    }

    private static Player FindMember(TemporaryPlayerTeam<ITeamMember<Player>> team, Player player, int memberObjId)
    {
        if (memberObjId == 0)
            return player;
        ITeamMember<Player> member = team.GetMember(memberObjId);
        if (member == null)
            throw new ArgumentNullException(null, player + " tried to execute team command on non-existent member with ID " + memberObjId);
        return member.GetObject();
    }
}
