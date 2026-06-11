using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WINDSTREAM_ANNOUNCE (LokiReborn). Announces windstream state (bidirectional/map/stream/state).</summary>
public class SM_WINDSTREAM_ANNOUNCE : AionServerPacket
{
    private int bidirectional;
    private int mapId;
    private int streamId;
    private int state;

    public SM_WINDSTREAM_ANNOUNCE(int bidirectional, int mapId, int streamId, int state)
    {
        this.bidirectional = bidirectional;
        this.mapId = mapId;
        this.streamId = streamId;
        this.state = state;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(bidirectional);
        WriteD(mapId);
        WriteD(streamId);
        WriteC(state);
    }
}
