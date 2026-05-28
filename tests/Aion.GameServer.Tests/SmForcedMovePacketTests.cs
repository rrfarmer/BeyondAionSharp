using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmForcedMovePacketTests
{
	[Fact]
	public void SmForcedMove_WritesJavaPayloadShape()
	{
		var packet = new SmForcedMove(new ForcedMoveSnapshot(SourceObjectId: 7001, TargetObjectId: 8002, X: 123.5f, Y: -45.25f, Z: 98.75f));

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmForcedMove.PacketOpCode, packet.OpCode);
		Assert.Equal(7001, reader.ReadD());
		Assert.Equal(8002, reader.ReadD());
		Assert.Equal(16, reader.ReadC());
		Assert.Equal(123.5f, reader.ReadF());
		Assert.Equal(-45.25f, reader.ReadF());
		Assert.Equal(98.75f, reader.ReadF());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
