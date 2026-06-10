using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_USE_OBJECT (ATracer). Use-object animation (player/target objIds + time + action type).</summary>
public class SM_USE_OBJECT : AionServerPacket
{
    private int playerObjId;
    private int targetObjId;
    private int time;
    private int actionType;

    public SM_USE_OBJECT(int playerObjId, int targetObjId, int time, int actionType)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = targetObjId;
        this.time = time;
        this.actionType = actionType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteD(targetObjId);
        WriteD(time);
        WriteC(actionType);
    }
}
