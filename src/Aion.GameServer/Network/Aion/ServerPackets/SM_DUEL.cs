using System;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DUEL (xavier). Duel started/result packet built via static factories. IllegalArgumentException -> ArgumentException; DuelResult red-tolerated.</summary>
public class SM_DUEL : AionServerPacket
{
    private string playerName;
    private DuelResult result;
    private int requesterObjId;
    private int type;

    private SM_DUEL(int type)
    {
        this.type = type;
    }

    public static SM_DUEL SM_DUEL_STARTED(int requesterObjId)
    {
        SM_DUEL packet = new SM_DUEL(0x00);
        packet.SetRequesterObjId(requesterObjId);
        return packet;
    }

    private void SetRequesterObjId(int requesterObjId)
    {
        this.requesterObjId = requesterObjId;
    }

    public static SM_DUEL SM_DUEL_RESULT(DuelResult result, string playerName)
    {
        SM_DUEL packet = new SM_DUEL(0x01);
        packet.SetPlayerName(playerName);
        packet.SetResult(result);
        return packet;
    }

    private void SetPlayerName(string playerName)
    {
        this.playerName = playerName;
    }

    private void SetResult(DuelResult result)
    {
        this.result = result;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(type);

        switch (type)
        {
            case 0x00:
                WriteD(requesterObjId);
                break;
            case 0x01:
                WriteC(result.GetResultId()); // unknown
                WriteD(result.GetMsgId());
                WriteS(playerName);
                break;
            case 0xE0:
                break;
            default:
                throw new ArgumentException("invalid SM_DUEL packet type " + type);
        }
    }
}
