using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmLegionEditTests
{
	[Fact]
	public void Level_WritesJavaTypeAndLevel()
	{
		using var reader = CreateReader(SmLegionEdit.Level(5));

		Assert.Equal(0x00, (int)reader.ReadC());
		Assert.Equal(5, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void RankingPosition_WritesJavaTypeAndPosition()
	{
		using var reader = CreateReader(SmLegionEdit.RankingPosition(12345));

		Assert.Equal(0x01, (int)reader.ReadC());
		Assert.Equal(12345, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Permissions_WritesJavaPermissionOrder()
	{
		using var reader = CreateReader(SmLegionEdit.Permissions(10, 20, 30, 40));

		Assert.Equal(0x02, (int)reader.ReadC());
		Assert.Equal(10, reader.ReadH());
		Assert.Equal(20, reader.ReadH());
		Assert.Equal(30, reader.ReadH());
		Assert.Equal(40, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Contribution_WritesJavaTypeAndContribution()
	{
		using var reader = CreateReader(SmLegionEdit.Contribution(77_000));

		Assert.Equal(0x03, (int)reader.ReadC());
		Assert.Equal(77_000, reader.ReadQ());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void WarehouseKinah_WritesJavaTypeAndKinah()
	{
		using var reader = CreateReader(SmLegionEdit.WarehouseKinah(123_456_789));

		Assert.Equal(0x04, (int)reader.ReadC());
		Assert.Equal(123_456_789, reader.ReadQ());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Announcement_WritesJavaMessageAndUnixTime()
	{
		using var reader = CreateReader(SmLegionEdit.Announcement("Assemble at the fortress", 1_771_234_567));

		Assert.Equal(0x05, (int)reader.ReadC());
		Assert.Equal("Assemble at the fortress", reader.ReadS());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Disband_WritesJavaTypeAndUnixTime()
	{
		using var reader = CreateReader(SmLegionEdit.Disband(1_771_234_567));

		Assert.Equal(0x06, (int)reader.ReadC());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Theory]
	[InlineData(0x07)]
	[InlineData(0x08)]
	public void EmptyEdits_WriteOnlyJavaType(int expectedType)
	{
		var packet = expectedType == 0x07
			? SmLegionEdit.Recover()
			: SmLegionEdit.RefreshAnnouncement();
		using var reader = CreateReader(packet);

		Assert.Equal(expectedType, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static PacketBuffer CreateReader(GameServerPacket packet)
	{
		return new PacketBuffer(SerializeUnencryptedPayload(packet));
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
