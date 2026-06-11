using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/ChangeAllianceLeaderEvent (ATracer) : ChangeLeaderEvent&lt;PlayerAlliance&gt;. Promotes a
/// vice-captain (or next available) to captain, demotes the old captain, broadcasts new-leader messages (league-aware). Uses
/// AssignViceCaptainEvent.AssignType. SM_*/PlayerAllianceService/league red-tolerated.
/// </summary>
public class ChangeAllianceLeaderEvent : ChangeLeaderEvent<PlayerAlliance>
{
    public ChangeAllianceLeaderEvent(PlayerAlliance team, Player eventPlayer)
        : base(team, eventPlayer)
    {
    }

    public ChangeAllianceLeaderEvent(PlayerAlliance team)
        : base(team, null)
    {
    }

    public override void HandleEvent()
    {
        if (eventPlayer == null)
        {
            foreach (int viceCaptainId in team.GetViceCaptainIds())
            {
                PlayerAllianceMember viceCaptain = team.GetMember(viceCaptainId);
                if (viceCaptain.IsOnline())
                {
                    ChangeLeaderTo(viceCaptain.GetObject());
                    return;
                }
            }
            ChangeLeaderToNextAvailablePlayer();
        }
        else
        {
            Player oldLeader = team.GetLeaderObject();
            ChangeLeaderTo(eventPlayer);
            PlayerAllianceService.ChangeViceCaptain(oldLeader, AssignViceCaptainEvent.AssignType.DEMOTE_CAPTAIN_TO_VICECAPTAIN);
        }
    }

    protected override void ChangeLeaderTo(Player player)
    {
        bool inLeague = team.IsInLeague();
        team.ChangeLeader(team.GetMember(player.GetObjectId()));
        team.GetViceCaptainIds().Remove(player.GetObjectId());
        if (inLeague)
        {
            team.GetLeague().Broadcast();
        }
        team.ForEach(member =>
        {
            if (!inLeague)
            {
                PacketSendUtility.SendPacket(member, new SM_ALLIANCE_INFO(team));
            }
            if (!player.Equals(member))
            {
                // eventPlayer null only when leader leave by own will and wee not must inform him about new leader
                if (eventPlayer != null)
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_FORCE_HE_IS_NEW_LEADER(player.GetName()));
                }
                if (inLeague && team.GetLeague().GetCaptain().Equals(player))
                {
                    team.GetLeague().ForEach(alliance =>
                    {
                        alliance.SendPacket(Predicates.Players.AllExcept(player), SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_LEADER_TIMEOUT(player.GetName()));
                    });
                }
            }
            else
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_FORCE_YOU_BECOME_NEW_LEADER());
                if (inLeague && team.GetLeague().GetCaptain().Equals(player))
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_UNION_YOU_BECOME_NEW_LEADER_TIMEOUT());
                }
            }
        });
    }
}
