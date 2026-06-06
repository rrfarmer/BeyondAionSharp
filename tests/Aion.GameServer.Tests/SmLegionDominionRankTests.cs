using Aion.Commons.Network;
using Aion.GameServer.Data;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmLegionDominionRankTests
{
	[Fact]
	public void OpcodeIs302()
	{
		// Java parity: ServerPacketsOpcodes addPacketOpcode(302, SM_LEGION_DOMINION_RANK.class).
		Assert.Equal(302, SmLegionDominionRank.PacketOpCode);
	}

	[Fact]
	public void WritesJavaPayloadSortedByPointsThenDate()
	{
		var packet = new SmLegionDominionRank(
			5,
			requestingLegionId: 20,
			[
				new LegionDominionParticipantRow(10, "Late", 100, 11, 2000),
				new LegionDominionParticipantRow(20, "Early", 100, 22, 1000),
				new LegionDominionParticipantRow(30, "Winner", 200, 33, 3000),
			]);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(5, buffer.ReadD());
		Assert.Equal(2, buffer.ReadC());
		Assert.Equal(3, buffer.ReadH());

		AssertParticipant(buffer, 200, 33, 3000, "Winner");
		AssertParticipant(buffer, 100, 22, 1000, "Early");
		AssertParticipant(buffer, 100, 11, 2000, "Late");
	}

	[Fact]
	public void ReplacesLastTopRowWithRequesterWhenRequesterRankIsOutsideTopTwentyFive()
	{
		var participants = new List<LegionDominionParticipantRow>();
		for (var i = 0; i < 26; i++)
			participants.Add(new LegionDominionParticipantRow(1000 + i, "Legion " + i, 100 - i, 10 + i, 2000 + i));

		var requester = new LegionDominionParticipantRow(77, "Requester", 1, 99, 9999);
		participants.Add(requester);

		var packet = new SmLegionDominionRank(6, requester.LegionId, participants);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(6, buffer.ReadD());
		Assert.Equal(27, buffer.ReadC());
		Assert.Equal(25, buffer.ReadH());

		for (var i = 0; i < 24; i++)
			AssertParticipant(buffer, 100 - i, 10 + i, 2000 + i, "Legion " + i);

		AssertParticipant(buffer, 1, 99, 9999, "Requester");
	}

	private static void AssertParticipant(PacketBuffer buffer, int points, int survivedTime, long epochSeconds, string legionName)
	{
		Assert.Equal(points, buffer.ReadD());
		Assert.Equal(survivedTime, buffer.ReadD());
		Assert.Equal(epochSeconds, buffer.ReadQ());
		Assert.Equal(legionName, buffer.ReadS());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
