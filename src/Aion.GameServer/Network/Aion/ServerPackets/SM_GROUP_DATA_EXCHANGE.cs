using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GROUP_DATA_EXCHANGE (xTz). Group blob exchange (action + optional unk + length-prefixed bytes).</summary>
public class SM_GROUP_DATA_EXCHANGE : AionServerPacket
{
    private byte[] byteData;
    private int action;
    private int unk2;

    public SM_GROUP_DATA_EXCHANGE(byte[] byteData, int action, int unk2)
    {
        this.action = action;
        this.byteData = byteData;
        this.unk2 = unk2;
    }

    public SM_GROUP_DATA_EXCHANGE(byte[] byteData)
    {
        this.action = 1;
        this.byteData = byteData;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action); // action

        if (action != 1)
            WriteC(unk2); // unk

        WriteD(byteData.Length);
        WriteB(byteData);
    }
}
