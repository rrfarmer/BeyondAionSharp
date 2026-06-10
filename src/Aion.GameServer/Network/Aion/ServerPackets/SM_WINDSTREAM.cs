using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WINDSTREAM. Windstream state (two unknown fields).</summary>
public class SM_WINDSTREAM : AionServerPacket
{
    private int unk1;
    private int unk2;

    public SM_WINDSTREAM(int unk1, int unk2)
    {
        this.unk1 = unk1;
        this.unk2 = unk2;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(unk1);
        WriteC(unk2);
    }
}
