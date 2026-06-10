using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>Java parity: model/team/league/events/LeagueInviteEvent (ATracer) : RequestResponseHandler&lt;Player&gt;. LeagueService/League red-tolerated.</summary>
public class LeagueInviteEvent : RequestResponseHandler<Player>
{
    private readonly Player invited;

    public LeagueInviteEvent(Player requester, Player invited) : base(requester)
    {
        this.invited = invited;
    }

    public override void AcceptRequest(Player requester, Player responder)
    {
        if (LeagueService.CanInvite(requester, invited))
        {
            League league = requester.GetPlayerAlliance().GetLeague();

            if (league == null)
            {
                league = LeagueService.CreateLeague(requester);
            }
            if (!invited.IsInLeague())
            {
                LeagueService.AddAlliance(league, invited.GetPlayerAlliance());
            }
        }
    }

    public override void DenyRequest(Player requester, Player responder)
    {
        PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_HE_REJECT_INVITATION(responder.GetName()));
    }
}
