using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INSTANCE_STAGE_INFO (xTz). Instance stage/event info. Field event->eventValue (C# keyword).</summary>
public class SM_INSTANCE_STAGE_INFO : AionServerPacket
{
    private int type;
    private int eventValue;
    private int unk;

    public SM_INSTANCE_STAGE_INFO(int type, int eventValue, int unk)
    {
        this.type = type;
        this.eventValue = eventValue;
        this.unk = unk;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(type);
        WriteD(0);
        WriteH(eventValue);
        WriteH(unk);
    }
}
