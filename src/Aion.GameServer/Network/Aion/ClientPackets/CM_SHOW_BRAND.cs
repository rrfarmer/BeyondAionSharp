using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SHOW_BRAND (Sweetkr, Simple). Sets a team brand (marker) on a target; self-only if no team, else leader/captain updates the team. TemporaryPlayerTeam wildcard -> var. SM_SHOW_BRAND red-tolerated.</summary>
public class CM_SHOW_BRAND : AionClientPacket
{
    private int action;
    private int brandId;
    private int targetObjectId;

    public CM_SHOW_BRAND(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadD();
        brandId = ReadD();
        targetObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        var team = player.GetCurrentTeam();
        if (team == null)
        {
            PacketSendUtility.SendPacket(player, new SM_SHOW_BRAND(brandId, targetObjectId));
        }
        else if (team.IsLeader(player) || team is PlayerAlliance alliance && alliance.IsSomeCaptain(player))
        {
            team.UpdateBrand(brandId, targetObjectId);
        }
    }
}
