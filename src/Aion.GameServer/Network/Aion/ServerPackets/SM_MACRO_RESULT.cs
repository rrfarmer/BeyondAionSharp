using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MACRO_RESULT (xavier). Macro create/delete result (static instances).</summary>
public class SM_MACRO_RESULT : AionServerPacket
{
    public static SM_MACRO_RESULT SM_MACRO_CREATED = new SM_MACRO_RESULT(0x00);
    public static SM_MACRO_RESULT SM_MACRO_DELETED = new SM_MACRO_RESULT(0x01);

    private int code;

    private SM_MACRO_RESULT(int code)
    {
        this.code = code;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(code);
    }
}
