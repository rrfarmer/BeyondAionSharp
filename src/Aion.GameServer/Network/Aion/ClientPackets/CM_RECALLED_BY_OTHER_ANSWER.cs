using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_RECALLED_BY_OTHER_ANSWER (SVDNESS). Answer to the SM_RECALLED_BY_OTHER window.</summary>
public class CM_RECALLED_BY_OTHER_ANSWER : AionClientPacket
{
    private int answer;

    public CM_RECALLED_BY_OTHER_ANSWER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        answer = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        player.GetResponseRequester().Respond(global::Aion.GameServer.Network.Aion.ServerPackets.SM_RECALLED_BY_OTHER.RECALL_REQUEST_ID, answer);
    }
}
