using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_RECALLED_BY_OTHER_ANSWER (SVDNESS). Answer to SM_RECALLED_BY_OTHER.</summary>
public class CM_RECALLED_BY_OTHER_ANSWER : AionClientPacket
{
    private int answer;

    public CM_RECALLED_BY_OTHER_ANSWER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        answer = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        switch (answer)
        {
            case 0:
                global::Aion.GameServer.Services.RecallService.GetInstance().Accept(player);
                break;
            case 1:
                global::Aion.GameServer.Services.RecallService.GetInstance().Cancel(player, global::Aion.GameServer.Services.RecallService.CancelReason.DECLINED);
                break;
            default:
                global::Aion.GameServer.Services.RecallService.GetInstance().Cancel(player, global::Aion.GameServer.Services.RecallService.CancelReason.CANCELLED);
                break;
        }
    }
}
