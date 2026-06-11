using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Capture;

/// <summary>Java parity: network/aion/capture/ServerPacketCaptureObserver. Observes serialized server packets.</summary>
public interface ServerPacketCaptureObserver
{
    bool IsEnabled();
    void OnPacketSerialized(AionConnection con, AionServerPacket packet, ByteBuffer clearFrame);
}
