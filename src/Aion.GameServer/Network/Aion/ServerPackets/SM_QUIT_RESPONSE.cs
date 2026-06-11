using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUIT_RESPONSE (-Nemesiss-). Response to CM_QUIT (1 normal, 2 plastic surgery/gender switch). Converges AntiHackService/PlayerTransfer disconnect. AionServerPacket red-tolerated.</summary>
public class SM_QUIT_RESPONSE : AionServerPacket
{
    private bool edit_mode = false;

    public SM_QUIT_RESPONSE()
    {
    }

    public SM_QUIT_RESPONSE(bool edit_mode)
    {
        this.edit_mode = edit_mode;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(edit_mode ? 2 : 1);// 1 normal, 2 plastic surgery/gender switch
        WriteC(0);// unk
        WriteD(-1);// unk 3.0
    }
}
