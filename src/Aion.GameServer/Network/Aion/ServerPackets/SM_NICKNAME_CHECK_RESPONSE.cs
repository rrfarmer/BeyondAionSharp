using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_NICKNAME_CHECK_RESPONSE (-Nemesiss-). Response for CM_CHECK_NICKNAME (0x00 ok, 0x0A not ok, etc).</summary>
public class SM_NICKNAME_CHECK_RESPONSE : AionServerPacket
{
    /// <summary>Value of response object</summary>
    private readonly int value;

    public SM_NICKNAME_CHECK_RESPONSE(int value)
    {
        this.value = value;
    }

    protected override void WriteImpl(AionConnection con)
    {
        // Here is some msg: 0x00 = ok 0x0A = not ok and much more
        WriteC(value);
    }
}
