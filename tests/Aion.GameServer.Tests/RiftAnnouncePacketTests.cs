using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RiftAnnouncePacketTests
{
	[Fact]
	public void Aggregate_WritesJavaShapedAnnouncePayload()
	{
		var packet = new SmRiftAnnounce(new RiftAnnounceData([1, 0, 0, 0, 2, 0, 3, 0, 0, 0, 0, 0]));

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(49, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(3, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Silentera_WritesJavaShapedWorldFlags()
	{
		var packet = new SmRiftAnnounce(gelkmaros: true, inggison: false);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(9, reader.ReadH());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Despawn_WritesJavaShapedObjectId()
	{
		var packet = new SmRiftAnnounce(123456);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(5, reader.ReadH());
		Assert.Equal(4, (int)reader.ReadC());
		Assert.Equal(123456, reader.ReadD());
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
