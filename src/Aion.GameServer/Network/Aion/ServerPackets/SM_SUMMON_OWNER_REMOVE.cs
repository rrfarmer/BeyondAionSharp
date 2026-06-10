using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SUMMON_OWNER_REMOVE (ATracer). Removes a summon from its owner's view (summon objId).</summary>
public class SM_SUMMON_OWNER_REMOVE : AionServerPacket
{
    private int summonObjId;

    public SM_SUMMON_OWNER_REMOVE(int summonObjId)
    {
        this.summonObjId = summonObjId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(summonObjId);
    }
}
