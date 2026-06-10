using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_QUESTION_RESPONSE (Ben, Sarynth, Neon). Response to SM_QUESTION_WINDOW; cancels exchange if answered yes mid-trade then dispatches the response. ExchangeService red-tolerated.</summary>
public class CM_QUESTION_RESPONSE : AionClientPacket
{
    private int questionid;
    private int response;
    private int senderid;

    public CM_QUESTION_RESPONSE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        questionid = ReadD();

        response = ReadUC(); // y/n
        ReadC(); // unk 0x00 - 0x01 ?
        ReadH();
        senderid = ReadD();
        ReadD();
        ReadH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTrading() && response != 0) // answered request with yes during exchange
            ExchangeService.GetInstance().CancelExchange(player);
        player.GetResponseRequester().Respond(questionid, response);
    }
}
