using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ICON_INFO (xTz). Toggles a buff icon display (buffId + display flag).</summary>
public class SM_ICON_INFO : AionServerPacket
{
    private int buffId;
    private bool display;

    public SM_ICON_INFO(int buffId, bool display)
    {
        this.buffId = buffId;
        this.display = display;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(0); // unk
        WriteD(buffId);
        WriteC(display ? 1 : 0);
    }
}
