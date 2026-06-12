using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Collections;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_INSTANCE_INFO (nrg, Neon). Sends instance score info for the team leader and (updateType 1) team members in groups of 3. SM_INSTANCE_INFO/SplitList red-tolerated.</summary>
public class CM_INSTANCE_INFO : AionClientPacket
{
    private byte updateType; // 0 = reset to client default values and overwrite, 1 = update team member info, 2 = overwrite only

    public CM_INSTANCE_INFO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadD(); // unk (always 0)
        updateType = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Player firstObject = player.IsInTeam() ? player.GetCurrentTeam().GetLeaderObject() : player; // always the team leader
        SendPacket(new SM_INSTANCE_INFO(updateType, firstObject));
        if (updateType == 1 && player.IsInTeam())
        {
            List<Player> filteredTeamMembers = player.GetCurrentTeam().FilterMembers(Predicates.Players.AllExcept(firstObject));
            SplitList<Player> playersSplitList = new FixedElementCountSplitList<Player>(filteredTeamMembers, false, 3);
            foreach (var part in playersSplitList)
                SendPacket(new SM_INSTANCE_INFO((byte)2, part));
        }
    }
}
