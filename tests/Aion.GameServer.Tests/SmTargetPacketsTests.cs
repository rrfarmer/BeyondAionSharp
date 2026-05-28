using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmTargetPacketsTests
{
	[Fact]
	public void SmTargetSelected_WritesZeroPayloadForNullTargetLikeJavaPrimitiveDefaults()
	{
		var packet = new SmTargetSelected(target: null);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmTargetSelected.PacketOpCode, packet.OpCode);
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmTargetSelected_WritesOnlyObjectIdForNonCreatureTargetLikeJava()
	{
		var payload = SerializeUnencryptedPayload(new SmTargetSelected(TargetSelectedSnapshot.VisibleObject(7001)));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(7001, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmTargetSelected_WritesCreatureStatsLikeJava()
	{
		var payload = SerializeUnencryptedPayload(new SmTargetSelected(
			new TargetSelectedSnapshot(
				TargetObjectId: 7002,
				Level: 55,
				MaxHp: 12000,
				CurrentHp: 9876,
				MaxMp: 4500,
				CurrentMp: 3210)));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(7002, reader.ReadD());
		Assert.Equal(55, reader.ReadH());
		Assert.Equal(12000, reader.ReadD());
		Assert.Equal(9876, reader.ReadD());
		Assert.Equal(4500, reader.ReadD());
		Assert.Equal(3210, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmTargetUpdate_WritesPlayerAndTargetObjectIdsLikeJava()
	{
		var payload = SerializeUnencryptedPayload(new SmTargetUpdate(playerObjectId: 1001, targetObjectId: 7002));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(81, SmTargetUpdate.PacketOpCode);
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(7002, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmTargetUpdate_UsesZeroTargetWhenPlayerHasNoTargetLikeJava()
	{
		var player = new Player
		{
			ObjectId = 1002,
			TargetObjectId = 0,
		};
		var payload = SerializeUnencryptedPayload(new SmTargetUpdate(player));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(1002, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
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
