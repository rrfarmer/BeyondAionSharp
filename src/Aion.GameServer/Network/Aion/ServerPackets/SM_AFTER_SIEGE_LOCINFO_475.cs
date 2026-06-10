using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_AFTER_SIEGE_LOCINFO_475 (Ritsu). Empty siege location info sent after enter-world. AionServerPacket red-tolerated.</summary>
public class SM_AFTER_SIEGE_LOCINFO_475 : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteH(0);
        WriteC(0);
    }
}
