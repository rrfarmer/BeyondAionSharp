using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PACKAGE_INFO_NOTIFY (Rolandas). Notifies of an account package (fixed fields + expiration time).</summary>
public class SM_PACKAGE_INFO_NOTIFY : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteH(1);
        WriteC(3);
        WriteD(0); // time until pack expiration
    }
}
