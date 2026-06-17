using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/PlayerDisconnectedEvent (ATracer) : TeamEvent. Transfers leadership if the leader
/// disconnected, broadcasts offline/disconnected member info, disbands if no online members remain or notifies league.
/// PlayerAllianceEvent enum; SM_*/PlayerAllianceService/league red-tolerated.
/// </summary>
public class PlayerDisconnectedEvent : ITeamEvent
{
    private readonly PlayerAlliance alliance;
    private readonly Player disconnected;
    private readonly PlayerAllianceMember disconnectedMember;

    public PlayerDisconnectedEvent(PlayerAlliance alliance, Player player)
    {
        this.alliance = alliance;
        this.disconnected = player;
        this.disconnectedMember = alliance.GetMember(disconnected.GetObjectId());
    }

    /// <summary>Player should be in alliance before disconnection</summary>
    public bool CheckCondition()
    {
        return alliance.HasMember(disconnected.GetObjectId());
    }

    public void HandleEvent()
    {
        if (disconnectedMember == null)
            throw new System.NullReferenceException("Disconnected member should not be null");
        Player leader = alliance.GetLeaderObject();

        if (disconnected.Equals(leader))
        {
            alliance.OnEvent(new ChangeAllianceLeaderEvent(alliance));
        }

        alliance.ForEach(member =>
        {
            if (!disconnected.Equals(member))
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_FORCE_HE_BECOME_OFFLINE(disconnected.GetName()));
                PacketSendUtility.SendPacket(member, new SM_ALLIANCE_MEMBER_INFO(disconnectedMember, PlayerAllianceEvent.DISCONNECTED));
                PacketSendUtility.SendPacket(member, new SM_ALLIANCE_INFO(alliance));
            }
        });

        if (alliance.GetOnlineMembers().Count == 0)
        {
            PlayerAllianceService.Disband(alliance, false);
        }
        else if (alliance.IsInLeague())
        {
            alliance.GetLeague().Broadcast(disconnected);
        }
    }
}
