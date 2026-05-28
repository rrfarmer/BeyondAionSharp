using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmLookAtObjectPacketTests
{
	[Fact]
	public void SmLookAtObject_WritesObjectTargetAndHeadingLikeJava()
	{
		var packet = new SmLookAtObject(new LookAtObjectSnapshot(
			ObjectId: 5001,
			TargetObjectId: 7002,
			Heading: 92));

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmLookAtObject.PacketOpCode, packet.OpCode);
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(7002, reader.ReadD());
		Assert.Equal(92, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmLookAtObject_WritesZeroTargetWhenJavaVisibleObjectHasNoTarget()
	{
		var packet = new SmLookAtObject(new LookAtObjectSnapshot(
			ObjectId: 5001,
			TargetObjectId: 0,
			Heading: 255));

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(255, reader.ReadC());
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
