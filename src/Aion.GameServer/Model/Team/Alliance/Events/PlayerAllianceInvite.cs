using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>Java parity: model/team/alliance/events/PlayerAllianceInvite (ATracer) : RequestResponseHandler&lt;Player&gt;. addAll→AddRange; IllegalArgumentException→ArgumentException; Predicates.Players.allExcept. PlayerAllianceService/PlayerGroupService/PlayerRestrictions red-tolerated.</summary>
public class PlayerAllianceInvite : RequestResponseHandler<Player>
{
    public PlayerAllianceInvite(Player inviter) : base(inviter)
    {
    }

    public override void AcceptRequest(Player inviter, Player invited)
    {
        if (PlayerRestrictions.CanInviteToAlliance(inviter, invited))
        {

            PlayerAlliance alliance = inviter.GetPlayerAlliance();
            List<Player> playersToAdd = new();
            CollectPlayersToAdd(inviter, invited, playersToAdd, alliance);

            if (alliance == null)
            {
                alliance = PlayerAllianceService.CreateAlliance(inviter, invited, TeamType.ALLIANCE);
                playersToAdd.Remove(invited);
            }

            foreach (Player member in playersToAdd)
            {
                PlayerAllianceService.AddPlayer(alliance, member);
            }
        }
    }

    private void CollectPlayersToAdd(Player inviter, Player invited, List<Player> playersToAdd, PlayerAlliance alliance)
    {
        // Collect requester Group without leader
        if (inviter.IsInGroup())
        {
            if (alliance != null)
                throw new ArgumentException("If requester is in group, alliance should be null");
            PlayerGroup group = inviter.GetPlayerGroup();
            playersToAdd.AddRange(group.FilterMembers(Predicates.Players.AllExcept(inviter)));

            foreach (Player player in group.GetMembers())
                PlayerGroupService.RemovePlayer(player);
        }

        // Collect full Invited Group
        if (invited.IsInGroup())
        {
            PlayerGroup group = invited.GetPlayerGroup();
            playersToAdd.AddRange(group.GetMembers());
            foreach (Player player in group.GetMembers())
                PlayerGroupService.RemovePlayer(player);
        }
        else // or just single player
        {
            playersToAdd.Add(invited);
        }
    }

    public override void DenyRequest(Player requester, Player responder)
    {
        PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_HE_REJECT_INVITATION(responder.GetName()));
    }
}
