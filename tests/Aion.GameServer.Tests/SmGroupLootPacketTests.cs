using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmGroupLootPacketTests
{
	[Fact]
	public void SmGroupLoot_WritesJavaPayload()
	{
		// Java parity: SM_GROUP_LOOT.writeImpl writes groupId(D), index(D), itemCount(D), itemId(D),
		// unk3(C=0), 0(C), 0(C), lootCorpseId(D), distributionId(C), playerId(D), luck(D as int).
		var packet = new SmGroupLoot(
			groupId: 1001, playerId: 2001, itemId: 30500, itemCount: 1,
			lootCorpseId: 5001, distributionId: 1, luck: 42, index: 3);

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(1001, reader.ReadD()); // groupId
		Assert.Equal(3, reader.ReadD());    // index
		Assert.Equal(1, reader.ReadD());    // itemCount
		Assert.Equal(30500, reader.ReadD()); // itemId
		Assert.Equal(0, (int)reader.ReadC()); // unk3
		Assert.Equal(0, (int)reader.ReadC()); // 3.0
		Assert.Equal(0, (int)reader.ReadC()); // 3.5
		Assert.Equal(5001, reader.ReadD()); // lootCorpseId
		Assert.Equal(1, (int)reader.ReadC()); // distributionId
		Assert.Equal(2001, reader.ReadD()); // playerId
		Assert.Equal(42, reader.ReadD());   // luck (cast to int)
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmGroupLoot_LuckTruncatedToInt()
	{
		// Java parity: writeD((int) luck) truncates long to int
		var packet = new SmGroupLoot(
			groupId: 1, playerId: 1, itemId: 1, itemCount: 1,
			lootCorpseId: 1, distributionId: 1, luck: 0xFFFFFFFFL, index: 0);

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		reader.ReadD(); // groupId
		reader.ReadD(); // index
		reader.ReadD(); // itemCount
		reader.ReadD(); // itemId
		reader.ReadC(); // unk3
		reader.ReadC(); reader.ReadC(); // version bytes
		reader.ReadD(); // lootCorpseId
		reader.ReadC(); // distributionId
		reader.ReadD(); // playerId
		// luck = (int)0xFFFFFFFFL = -1 when read as signed int
		Assert.Equal(-1, reader.ReadD());
	}

	[Fact]
	public void SmGroupLoot_OpcodeIs135()
	{
		Assert.Equal(135, SmGroupLoot.PacketOpCode);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
