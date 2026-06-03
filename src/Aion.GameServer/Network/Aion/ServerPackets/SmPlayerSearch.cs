using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

// Java parity: network/aion/serverpackets/SM_PLAYER_SEARCH.writeImpl. Result rows projected from Player.
public sealed record PlayerSearchResultRow(
	int WorldId,
	float X,
	float Y,
	float Z,
	int ClassId,
	int GenderId,
	int Level,
	int Status, // 1 = denied group, 3 = in team, 2 = looking for group, 0 = available
	string Name);

public sealed class SmPlayerSearch : GameServerPacket
{
	public const int PacketOpCode = 211;

	// Java parity: AbstractPlayerInfoPacket.CHARNAME_MAX_LENGTH = 25; writeS uses CHARNAME_MAX_LENGTH + 2.
	private const int NameFixedLength = 27;

	private readonly IReadOnlyList<PlayerSearchResultRow> _results;

	public SmPlayerSearch(IReadOnlyList<PlayerSearchResultRow> results)
		: base(PacketOpCode)
	{
		_results = results;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_PLAYER_SEARCH.writeImpl.
		buffer.WriteH(_results.Count);
		foreach (var row in _results)
		{
			buffer.WriteD(row.WorldId);
			buffer.WriteF(row.X);
			buffer.WriteF(row.Y);
			buffer.WriteF(row.Z);
			buffer.WriteC(row.ClassId);
			buffer.WriteC(row.GenderId);
			buffer.WriteC(row.Level);
			buffer.WriteC(row.Status);
			WriteFixedS(buffer, row.Name, NameFixedLength);
		}
	}

	private static void WriteFixedS(PacketBuffer buffer, string? value, int fixedLength)
	{
		// Java parity: AionServerPacket.writeS(text, fixedLength) — zero-padded/truncated UTF-16 + terminator.
		for (var i = 0; i < fixedLength; i++)
		{
			var c = !string.IsNullOrEmpty(value) && i < value.Length ? value[i] : '\0';
			buffer.WriteH(c);
		}

		buffer.WriteH(0);
	}
}
