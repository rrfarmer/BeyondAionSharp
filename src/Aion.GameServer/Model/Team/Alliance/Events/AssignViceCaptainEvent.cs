using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/AssignViceCaptainEvent (ATracer) : AbstractTeamPlayerEvent&lt;PlayerAlliance&gt;. Promotes/
/// demotes alliance vice-captains. Nested enum AssignType SCREAMING (kept). team/eventPlayer are protected base fields. ViceCaptainIds
/// set ops; SM_ALLIANCE_INFO.VICECAPTAIN_* message ids; SM_SYSTEM_MESSAGE.STR_*/league red-tolerated.
/// </summary>
public class AssignViceCaptainEvent : AbstractTeamPlayerEvent<PlayerAlliance>
{
    public enum AssignType
    {
        PROMOTE,
        DEMOTE_CAPTAIN_TO_VICECAPTAIN,
        DEMOTE
    }

    private readonly AssignType assignType;

    public AssignViceCaptainEvent(PlayerAlliance team, Player eventPlayer, AssignType assignType)
        : base(team, eventPlayer)
    {
        this.assignType = assignType;
    }

    public override bool CheckCondition()
    {
        return eventPlayer != null && eventPlayer.IsOnline();
    }

    public override void HandleEvent()
    {
        switch (assignType)
        {
            case AssignType.DEMOTE:
                team.GetViceCaptainIds().Remove(eventPlayer.GetObjectId());
                break;
            case AssignType.PROMOTE:
                if (team.GetViceCaptainIds().Count == 4)
                {
                    PacketSendUtility.SendPacket(team.GetLeaderObject(), SM_SYSTEM_MESSAGE.STR_FORCE_CANNOT_PROMOTE_MANAGER());
                    return;
                }
                team.GetViceCaptainIds().Add(eventPlayer.GetObjectId());
                break;
            case AssignType.DEMOTE_CAPTAIN_TO_VICECAPTAIN:
                if (team.GetViceCaptainIds().Count < 3)
                {
                    team.GetViceCaptainIds().Add(eventPlayer.GetObjectId());
                }
                break;
        }

        team.ForEach(member =>
        {
            int messageId = 0;
            switch (assignType)
            {
                case AssignType.PROMOTE:
                    messageId = SM_ALLIANCE_INFO.VICECAPTAIN_PROMOTE;
                    break;
                case AssignType.DEMOTE:
                    messageId = SM_ALLIANCE_INFO.VICECAPTAIN_DEMOTE;
                    break;
            }
            // TODO check whether same is sent to eventPlayer
            PacketSendUtility.SendPacket(member, new SM_ALLIANCE_INFO(team, messageId, eventPlayer.GetName()));
        });

        if (team.IsInLeague())
        {
            team.GetLeague().Broadcast();
        }
    }
}
