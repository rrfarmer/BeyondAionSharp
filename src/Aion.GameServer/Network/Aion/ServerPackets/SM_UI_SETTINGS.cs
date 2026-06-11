using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_UI_SETTINGS (ATracer). Sends UI/shortcut/house-buddy settings blob padded to 0x1C00 bytes. Converges PlayerEnterWorldService. AionServerPacket red-tolerated.</summary>
public class SM_UI_SETTINGS : AionServerPacket
{
    private byte[] data;
    private int type;

    public SM_UI_SETTINGS(byte[] data, int type)
    {
        this.data = data;
        this.type = type;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(type);
        WriteH(0x1C00);
        WriteB(data);
        if (0x1C00 > data.Length)
            WriteB(new byte[0x1C00 - data.Length]);
    }
}
