using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

// Java parity: model/team/legion/LegionHistoryEntry projected to the wire fields used by SM_LEGION_HISTORY.writeImpl.
public sealed record LegionHistoryEntryRow(
	int EpochSeconds,
	byte ActionId, // Java parity: LegionHistoryAction.getId() (CREATE=0 .. KINAH_WITHDRAW=18).
	string Name,
	string Description);

// Java parity: network/aion/serverpackets/SM_LEGION_HISTORY.
public sealed class SmLegionHistory : GameServerPacket
{
	public const int PacketOpCode = 12; // Java parity: ServerPacketsOpcodes addPacketOpcode(12, SM_LEGION_HISTORY.class).

	// Java parity: SM_LEGION_HISTORY.ENTRIES_PER_PAGE = 8.
	private const int EntriesPerPage = 8;

	// Java parity: writeS(name/description, 32).
	private const int NameFixedLength = 32;

	private readonly int _totalEntries;
	private readonly int _page;
	private readonly int _typeOrdinal;
	private readonly IReadOnlyList<LegionHistoryEntryRow> _pageEntries;

	public SmLegionHistory(IReadOnlyList<LegionHistoryEntryRow> history, int page, int typeOrdinal)
		: base(PacketOpCode)
	{
		// Java parity: SM_LEGION_HISTORY(List<LegionHistoryEntry> history, int page, Type type).
		_totalEntries = history.Count;
		_page = page;
		_typeOrdinal = typeOrdinal;
		_pageEntries = FindEntriesForCurrentPage(history, page);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_LEGION_HISTORY.writeImpl.
		buffer.WriteD(_totalEntries);
		buffer.WriteD(_page); // current page
		buffer.WriteD(_pageEntries.Count);
		foreach (var entry in _pageEntries)
		{
			buffer.WriteD(entry.EpochSeconds);
			buffer.WriteC(entry.ActionId);
			buffer.WriteC(0); // unk
			WriteFixedS(buffer, entry.Name, NameFixedLength);
			WriteFixedS(buffer, entry.Description, NameFixedLength);
			buffer.WriteH(0);
		}

		buffer.WriteH(_typeOrdinal);
	}

	private static IReadOnlyList<LegionHistoryEntryRow> FindEntriesForCurrentPage(IReadOnlyList<LegionHistoryEntryRow> history, int page)
	{
		// Java parity: SM_LEGION_HISTORY.findEntriesForCurrentPage.
		var startIndex = page * EntriesPerPage;
		if (startIndex < 0 || startIndex >= history.Count)
			return Array.Empty<LegionHistoryEntryRow>();

		var endIndex = Math.Min(startIndex + EntriesPerPage, history.Count);
		var slice = new List<LegionHistoryEntryRow>(endIndex - startIndex);
		for (var i = startIndex; i < endIndex; i++)
			slice.Add(history[i]);

		return slice;
	}

	private static void WriteFixedS(PacketBuffer buffer, string? value, int fixedLength)
	{
		// Java parity: AionServerPacket.writeS(text, fixedLength) — fixedLength UTF-16 chars (zero-padded/truncated) + terminator.
		// Java's empty-string branch writes (fixedLength + 1) zero chars, which is identical to this loop when value is empty.
		for (var i = 0; i < fixedLength; i++)
		{
			var c = !string.IsNullOrEmpty(value) && i < value.Length ? value[i] : '\0';
			buffer.WriteH(c);
		}

		buffer.WriteH(0);
	}
}
