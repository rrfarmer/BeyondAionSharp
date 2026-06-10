using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ASCENSION_MORPH (wylovech). Ascension quest morph flag (1 = morph).</summary>
public class SM_ASCENSION_MORPH : AionServerPacket
{
    private int inascension;

    public SM_ASCENSION_MORPH(int inascension)
    {
        this.inascension = inascension;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(inascension);// if inascension =0x01 morph.
        WriteC(0x00); // new 2.0 Packet --- probably pet info?
    }
}
