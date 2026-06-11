using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Capture;

/// <summary>
/// Java parity: network/aion/capture/NoOpServerPacketCaptureObserver (enum singleton INSTANCE) ->
/// C# singleton class (C# enums can't implement interface methods). No-op observer.
/// </summary>
public sealed class NoOpServerPacketCaptureObserver : ServerPacketCaptureObserver
{
    public static readonly NoOpServerPacketCaptureObserver INSTANCE = new NoOpServerPacketCaptureObserver();

    private NoOpServerPacketCaptureObserver() { }

    public bool IsEnabled() => false;

    public void OnPacketSerialized(AionConnection con, AionServerPacket packet, ByteBuffer clearFrame) { }
}
