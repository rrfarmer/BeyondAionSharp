using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_INVITE_TO_GROUP (Lyahim, ATracer, Simple, Neon). Invites a player to group (0) / alliance (12) / league (28) with dead/offline/deny guards. PlayerGroupService/PlayerAllianceService/LeagueService red-tolerated.</summary>
public class CM_INVITE_TO_GROUP : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_INVITE_TO_GROUP));
    private string playerName;
    private int inviteType;

    public CM_INVITE_TO_GROUP(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        inviteType = ReadUC();
        playerName = ReadS();
    }

    protected override void RunImpl()
    {
        Player inviter = GetConnection().GetActivePlayer();
        if (inviter.IsDead())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_PARTY_CANT_INVITE_WHEN_DEAD());
            return;
        }

        Player invited = World.GetInstance().GetPlayer(ChatUtil.GetRealCharName(playerName));
        if (invited == null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }

        if (invited.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.GROUP))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_INVITE_PARTY(invited.GetName(true)));
            return;
        }

        switch (inviteType)
        {
            case 0:
                PlayerGroupService.InviteToGroup(inviter, invited);
                break;
            case 12: // 2.5
                PlayerAllianceService.InviteToAlliance(inviter, invited);
                break;
            case 28:
                LeagueService.InviteToLeague(inviter, invited);
                break;
            default:
                log.LogWarning("Received unknown invite type from player " + inviter.GetName() + ": " + inviteType);
                break;
        }
    }
}
