using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.Commons.Logging;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class PacketWarningAndFloodModesPortTests
{
    [Fact]
    public void RepeatedConcurrentShortReadsStillRejectEveryPacketAndWarnOncePerOpcode()
    {
        using var capture = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        using var logging = Aion.Commons.Logging.AionLog.OverrideFactory(factory);
        BaseClientPacket<ProbeContext>.ResetPartiallyReadPacketWarnings();
        Parallel.For(0, 64, _ => Assert.False(Read(12340, Array.Empty<byte>())));
        Assert.False(Read(12341, new byte[] { 0xAA }));
        Assert.False(Read(12341, new byte[] { 0xBB }));
        var other = new OtherContextProbe(12340); other.SetConnection("other connection type");
        other.SetBuffer(ByteBuffer.Wrap(Array.Empty<byte>())); Assert.False(other.Read());
        Assert.Equal(2, capture.Entries.Count(e => e.Message.Contains("shorter than expected")));
        Assert.Contains(capture.Entries, e => e.Message.Contains("AA"));
        Assert.True(Read(12340, new byte[4])); // warning suppression never changes parsing/rejection
        Assert.True(Read(12340, new byte[4], readByte: true)); // partial-read warning has its own gate
        Assert.Single(capture.Entries, e => e.Message.Contains("was not fully read"));
        BaseClientPacket<ProbeContext>.ResetPartiallyReadPacketWarnings();
        Assert.False(Read(12340, Array.Empty<byte>()));
        Assert.Equal(3, capture.Entries.Count(e => e.Message.Contains("shorter than expected")));
    }

    /// <summary>
    /// Upstream 55d0af937 made mode 1 drop the flooding packet as well as warn about it, so a flooded packet is no
    /// longer executed in any mode: mode 1 keeps the connection and skips the packet, mode 2 disconnects.
    /// </summary>
    [Theory]
    [InlineData(0, 1, true, 2)] // off: the packet runs
    [InlineData(1, 1, true, 1)] // flooding, dropped: connection kept, packet not executed
    [InlineData(2, 1, false, 1)] // flooding, disconnected
    [InlineData(2, 100, true, 2)] // within the threshold
    public void RealEncryptedPacketsUseModeOneForWarningsAndModeTwoForDisconnects(int mode, int elapsed, bool accepted, int executed)
    {
        int previousMode = PffConfig.PFF_MODE;
        var previousThresholds = PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE;
        long now = 100000;
        using var capture = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        using var logging = Aion.Commons.Logging.AionLog.OverrideFactory(factory);
        SystemClock.UseSource(() => now);
        try
        {
            PffConfig.PFF_MODE = mode;
            PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE = new Dictionary<int, int> { [44] = 100 };
            var connection = new Connection(); connection.SetState(AionConnection.State.AUTHED);
            var codec = new GamePacketCodec(); connection.InitializedInternal();
            var keyBuffer = ByteBuffer.Allocate(1024).Order(ByteOrder.LITTLE_ENDIAN);
            Assert.True(connection.WriteDataInternal(keyBuffer));
            var keyFrame = new byte[keyBuffer.Limit()]; keyBuffer.Get(keyFrame); codec.RecoverKeyFromSmKeyFrame(keyFrame);
            bool Send()
            {
                byte[] frame = GameClientPackets.Ping().Encode(codec, AionConnection.State.AUTHED);
                return connection.ProcessDataInternal(ByteBuffer.Wrap(frame[2..]).Order(ByteOrder.LITTLE_ENDIAN));
            }
            Assert.True(Send()); now += elapsed;
            Assert.Equal(accepted, Send()); Assert.Equal(executed, connection.Executed);
            Assert.Equal(mode > 0 && elapsed < 100 ? 1 : 0, capture.Entries.Count(e => e.Message.Contains("is flooding")));
        }
        finally
        {
            PffConfig.PFF_MODE = previousMode; PffConfig.THRESHOLD_MILLIS_BY_PACKET_OPCODE = previousThresholds;
            SystemClock.UseSystemClock();
        }
    }

    private static bool Read(int opcode, byte[] body, bool readByte = false)
    {
        var packet = new Probe(opcode, readByte); packet.SetConnection(new ProbeContext());
        packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN)); return packet.Read();
    }
    private sealed class ProbeContext { }
    private sealed class Probe(int opcode, bool readByte) : BaseClientPacket<ProbeContext>(opcode)
    {
        protected override void ReadImpl() { if (readByte) ReadC(); else ReadD(); }
        protected override void RunImpl() { }
        public override void Run() { }
    }
    private sealed class OtherContextProbe(int opcode) : BaseClientPacket<string>(opcode)
    {
        protected override void ReadImpl() => ReadD();
        protected override void RunImpl() { }
        public override void Run() { }
    }
    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        public int Executed;
        protected override bool ExecutePacket(AionClientPacket packet) { Executed++; return true; }
    }
}
