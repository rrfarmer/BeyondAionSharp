using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/PlayerConnectedEvent (ATracer) : AlwaysTrueTeamEvent. Re-adds a reconnecting alliance
/// member, rebroadcasts alliance/member info (RECONNECT). PlayerAllianceEvent enum; SM_ALLIANCE_* red-tolerated.
/// </summary>
public class PlayerConnectedEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerAlliance alliance;
    private readonly Player connected;

    public PlayerConnectedEvent(PlayerAlliance alliance, Player player)
    {
        this.alliance = alliance;
        this.connected = player;
    }

    public override void HandleEvent()
    {
        alliance.RemoveMember(connected.GetObjectId());
        PlayerAllianceMember connectedMember = new PlayerAllianceMember(connected);
        alliance.AddMember(connectedMember);

        PacketSendUtility.SendPacket(connected, new SM_ALLIANCE_INFO(alliance));
        PacketSendUtility.SendPacket(connected, new SM_ALLIANCE_MEMBER_INFO(connectedMember, PlayerAllianceEvent.RECONNECT));
        alliance.ForEachTeamMember(member =>
        {
            Player player = member.GetObject();
            if (!connected.Equals(player))
            {
                PacketSendUtility.SendPacket(player, new SM_ALLIANCE_MEMBER_INFO(connectedMember, PlayerAllianceEvent.RECONNECT));
                PacketSendUtility.SendPacket(connected, new SM_ALLIANCE_MEMBER_INFO(member, PlayerAllianceEvent.RECONNECT));
            }
        });
    }
}
