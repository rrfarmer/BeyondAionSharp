using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RESTORE_CHARACTER (-Nemesiss-). Response for CM_RESTORE_CHARACTER (success flag + char objId).</summary>
public class SM_RESTORE_CHARACTER : AionServerPacket
{
    /// <summary>Character object id.</summary>
    private readonly int chaOid;

    /// <summary>True if player was restored.</summary>
    private readonly bool success;

    public SM_RESTORE_CHARACTER(int chaOid, bool success)
    {
        this.chaOid = chaOid;
        this.success = success;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(success ? 0x00 : 0x10);// unk
        WriteD(chaOid);
    }
}
