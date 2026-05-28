using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmPositionPacketsTests
{
	[Fact]
	public void SmPosition_WritesObjectPositionAndHeadingLikeJava()
	{
		var packet = new SmPosition(new ObjectPositionSnapshot(
			ObjectId: 5001,
			X: 123.5f,
			Y: -45.25f,
			Z: 98.75f,
			Heading: 105));

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmPosition.PacketOpCode, packet.OpCode);
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(123.5f, reader.ReadF());
		Assert.Equal(-45.25f, reader.ReadF());
		Assert.Equal(98.75f, reader.ReadF());
		Assert.Equal(105, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmPositionSelf_WritesCoordinatesAndHeadingLikeJava()
	{
		var packet = new SmPositionSelf(new PositionSelfSnapshot(
			X: 123.5f,
			Y: -45.25f,
			Z: 98.75f,
			Heading: 105));

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmPositionSelf.PacketOpCode, packet.OpCode);
		Assert.Equal(123.5f, reader.ReadF());
		Assert.Equal(-45.25f, reader.ReadF());
		Assert.Equal(98.75f, reader.ReadF());
		Assert.Equal(105, reader.ReadC());
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
