using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmGroupDataExchangeTests
{
	[Fact]
	public void PacketOpCode_MatchesJavaServerOpcode()
	{
		// Java parity: ServerPacketsOpcodes.addPacketOpcode(178, SM_GROUP_DATA_EXCHANGE.class).
		Assert.Equal(178, SmGroupDataExchange.PacketOpCode);
	}

	[Fact]
	public void WritePayload_NearbyBroadcastMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(SmGroupDataExchange.NearbyBroadcast([1, 2, 255]));

		Assert.Equal(Convert.FromHexString("01030000000102FF"), payload);
	}

	[Fact]
	public void WritePayload_GroupBroadcastMatchesJavaGolden()
	{
		var payload = SerializeUnencryptedPayload(SmGroupDataExchange.GroupBroadcast([10, 11, 12, 13], 2, 7));

		Assert.Equal(Convert.FromHexString("0207040000000A0B0C0D"), payload);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
