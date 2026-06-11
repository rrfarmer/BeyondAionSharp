using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>Java parity: model/team/group/events/PlayerGroupInvite (ATracer) : RequestResponseHandler&lt;Player&gt;. acceptRequest/denyRequest→override. PlayerGroupService/PlayerRestrictions/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class PlayerGroupInvite : RequestResponseHandler<Player>
{
    public PlayerGroupInvite(Player inviter) : base(inviter)
    {
    }

    public override void AcceptRequest(Player inviter, Player invited)
    {
        if (PlayerRestrictions.CanInviteToGroup(inviter, invited))
        {
            PlayerGroup group = inviter.GetPlayerGroup();
            if (group != null)
            {
                PlayerGroupService.AddPlayer(group, invited);
            }
            else
            {
                PlayerGroupService.CreateGroup(inviter, invited, TeamType.GROUP, 0);
            }
        }
    }

    public override void DenyRequest(Player inviter, Player invited)
    {
        PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_PARTY_HE_REJECT_INVITATION(invited.GetName()));
    }
}
