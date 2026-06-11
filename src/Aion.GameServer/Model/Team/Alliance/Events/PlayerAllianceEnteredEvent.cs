using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/PlayerAllianceEnteredEvent (ATracer) : PlayerEnteredEvent&lt;PlayerAlliance&gt;. Adds player
/// to alliance and broadcasts join/enter member-info + force messages (league-aware). PlayerAllianceEvent.JOIN/ENTER -> Join/Enter;
/// forEachTeamMember -> ForEachTeamMember (member.GetObject()). PlayerAllianceService/SM_* red-tolerated.
/// </summary>
public class PlayerAllianceEnteredEvent : PlayerEnteredEvent<PlayerAlliance>
{
    public PlayerAllianceEnteredEvent(PlayerAlliance alliance, Player player)
        : base(alliance, player)
    {
    }

    public override void HandleEvent()
    {
        PlayerAllianceMember invitedMember = PlayerAllianceService.AddPlayerToAlliance(team, player);

        SM_ALLIANCE_INFO allianceInfo = new SM_ALLIANCE_INFO(team);
        SM_ALLIANCE_MEMBER_INFO allianceMemberInfo = new SM_ALLIANCE_MEMBER_INFO(invitedMember, PlayerAllianceEvent.Join);
        PacketSendUtility.SendPacket(player, allianceInfo);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FORCE_ENTERED_FORCE());
        PacketSendUtility.SendPacket(player, allianceMemberInfo);
        team.SendBrands(player);
        team.ForEachTeamMember(member =>
        {
            Player p = member.GetObject();
            if (!player.Equals(p))
            {
                PacketSendUtility.SendPacket(p, allianceMemberInfo);
                PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_FORCE_HE_ENTERED_FORCE(player.GetName()));
                PacketSendUtility.SendPacket(p, allianceInfo);
                PacketSendUtility.SendPacket(player, new SM_ALLIANCE_MEMBER_INFO(member, PlayerAllianceEvent.Enter));
            }
        });
        PacketSendUtility.BroadcastPacket(player, new SM_ABYSS_RANK_UPDATE(1, player), true);

        if (team.IsInLeague())
        {
            team.GetLeague().Broadcast();
        }
        base.HandleEvent();
    }
}
