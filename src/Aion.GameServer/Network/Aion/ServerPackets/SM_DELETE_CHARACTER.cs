using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DELETE_CHARACTER (-Nemesiss-). Response for CM_DELETE_CHARACTER: writes objId + deletion time, or a failure triple when objId == 0.</summary>
public class SM_DELETE_CHARACTER : AionServerPacket
{
    private int playerObjId;
    private int deletionTime;

    /// <summary>
    /// Constructs new <c>SM_DELETE_CHARACTER</c> packet
    /// </summary>
    public SM_DELETE_CHARACTER(int playerObjId, int deletionTime)
    {
        this.playerObjId = playerObjId;
        this.deletionTime = deletionTime;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (playerObjId != 0)
        {
            WriteD(0x00);// unk
            WriteD(playerObjId);
            WriteD(deletionTime);
        }
        else
        {
            WriteD(0x10);// unk
            WriteD(0x00);
            WriteD(0x00);
        }
    }
}
