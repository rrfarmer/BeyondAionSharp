using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INSTANCE_COUNT_INFO (xTz). Instance entry-count info (map/instance id + group-type marker).</summary>
public class SM_INSTANCE_COUNT_INFO : AionServerPacket
{
    private int mapId;
    private int instanceId;

    public SM_INSTANCE_COUNT_INFO(int mapId, int instanceId)
    {
        this.mapId = mapId;
        this.instanceId = instanceId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(mapId);
        WriteD(instanceId);
        WriteD(1); // 1 solo 31 group 61 alliance unk for league
    }
}
