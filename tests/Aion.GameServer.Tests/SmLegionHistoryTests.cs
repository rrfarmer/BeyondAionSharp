using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmLegionHistoryTests
{
	[Fact]
	public void OpcodeIs12()
	{
		// Java parity: ServerPacketsOpcodes addPacketOpcode(12, SM_LEGION_HISTORY.class).
		Assert.Equal(12, SmLegionHistory.PacketOpCode);
	}

	[Fact]
	public void EmptyHistory_WritesHeaderAndTypeOrdinalOnly()
	{
		// Java parity: writeImpl with empty history -> totalEntries=0, page, pageEntries.size()=0, then type.ordinal().
		var packet = new SmLegionHistory(Array.Empty<LegionHistoryEntryRow>(), page: 0, typeOrdinal: 1);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(0, buffer.ReadD()); // totalEntries
		Assert.Equal(0, buffer.ReadD()); // page
		Assert.Equal(0, buffer.ReadD()); // pageEntries count
		Assert.Equal(1, buffer.ReadH()); // type ordinal (REWARD = 1)
	}

	[Fact]
	public void SingleEntry_WritesEntryFieldsAndFixedStrings()
	{
		// Java parity: per-entry layout = epochSeconds(D), actionId(C), 0(C), name(S,32), description(S,32), H(0); trailing type ordinal(H).
		var entry = new LegionHistoryEntryRow(EpochSeconds: 1717459200, ActionId: 2 /* KICK */, Name: "Hi", Description: "d");
		var packet = new SmLegionHistory(new[] { entry }, page: 0, typeOrdinal: 0);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(1, buffer.ReadD()); // totalEntries
		Assert.Equal(0, buffer.ReadD()); // page
		Assert.Equal(1, buffer.ReadD()); // pageEntries count

		Assert.Equal(1717459200, buffer.ReadD()); // epochSeconds
		Assert.Equal(2, buffer.ReadC()); // actionId
		Assert.Equal(0, buffer.ReadC()); // unk

		// name: 32 fixed UTF-16 chars (zero padded) + terminator.
		Assert.Equal('H', (char)buffer.ReadH());
		Assert.Equal('i', (char)buffer.ReadH());
		for (var i = 2; i < 32; i++)
			Assert.Equal(0, buffer.ReadH());
		Assert.Equal(0, buffer.ReadH()); // name terminator

		// description: 32 fixed chars + terminator.
		Assert.Equal('d', (char)buffer.ReadH());
		for (var i = 1; i < 32; i++)
			Assert.Equal(0, buffer.ReadH());
		Assert.Equal(0, buffer.ReadH()); // description terminator

		Assert.Equal(0, buffer.ReadH()); // trailing per-entry H(0)
		Assert.Equal(0, buffer.ReadH()); // type ordinal (LEGION = 0)
	}

	[Fact]
	public void Paging_SlicesEightPerPageAndReportsFullTotal()
	{
		// Java parity: findEntriesForCurrentPage — ENTRIES_PER_PAGE=8; page 1 of 10 yields entries [8,10).
		var history = new List<LegionHistoryEntryRow>();
		for (var i = 0; i < 10; i++)
			history.Add(new LegionHistoryEntryRow(EpochSeconds: i, ActionId: 0, Name: "n" + i, Description: string.Empty));

		var packet = new SmLegionHistory(history, page: 1, typeOrdinal: 2);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(10, buffer.ReadD()); // totalEntries = full history size
		Assert.Equal(1, buffer.ReadD()); // page
		Assert.Equal(2, buffer.ReadD()); // pageEntries count (entries 8 and 9)

		// First entry on page 1 is original index 8.
		Assert.Equal(8, buffer.ReadD()); // epochSeconds of entry index 8
	}

	[Fact]
	public void PageBeyondEnd_WritesEmptyPageButFullTotal()
	{
		// Java parity: startIndex >= size -> Collections.emptyList(), but totalEntries still reports full size.
		var history = new List<LegionHistoryEntryRow>
		{
			new(EpochSeconds: 5, ActionId: 0, Name: "a", Description: "b"),
		};

		var packet = new SmLegionHistory(history, page: 3, typeOrdinal: 0);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(1, buffer.ReadD()); // totalEntries
		Assert.Equal(3, buffer.ReadD()); // page
		Assert.Equal(0, buffer.ReadD()); // pageEntries count (out of range)
		Assert.Equal(0, buffer.ReadH()); // type ordinal
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..]; // skip 2-byte length + 2-byte opcode + 1-byte static code + 2-byte ~opcode
	}
}
