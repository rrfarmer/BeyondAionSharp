using Aion.Commons.Network;
using Aion.GameServer.Data;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed record LegionDominionRankEntry(
	int LegionId,
	string LegionName,
	int Points,
	int SurvivedTime,
	long ParticipatedEpochSeconds);

// Java parity: network/aion/serverpackets/SM_LEGION_DOMINION_RANK.
public sealed class SmLegionDominionRank : GameServerPacket
{
	public const int PacketOpCode = 302; // Java parity: ServerPacketsOpcodes addPacketOpcode(302, SM_LEGION_DOMINION_RANK.class).

	private readonly int _locationId;
	private readonly int _rank;
	private readonly IReadOnlyList<LegionDominionRankEntry> _topParticipants;

	public SmLegionDominionRank(
		int locationId,
		int requestingLegionId,
		IReadOnlyList<LegionDominionParticipantRow> participants)
		: base(PacketOpCode)
	{
		_locationId = locationId;
		var ranking = participants
			.OrderByDescending(participant => participant.Points)
			.ThenBy(participant => participant.ParticipatedEpochSeconds)
			.Select(participant => new LegionDominionRankEntry(
				participant.LegionId,
				string.IsNullOrEmpty(participant.LegionName) ? "NOT AVAILABLE" : participant.LegionName,
				participant.Points,
				participant.SurvivedTime,
				participant.ParticipatedEpochSeconds))
			.ToList();

		var participantIndex = requestingLegionId <= 0
			? -1
			: ranking.FindIndex(participant => participant.LegionId == requestingLegionId);
		_rank = participantIndex < 0 ? 0 : participantIndex + 1;
		_topParticipants = SelectTopParticipants(ranking, _rank);
	}

	public int LocationId => _locationId;

	public int Rank => _rank;

	public IReadOnlyList<LegionDominionRankEntry> TopParticipants => _topParticipants;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_locationId);
		buffer.WriteC(_rank);
		buffer.WriteH(_topParticipants.Count);
		foreach (var participant in _topParticipants)
		{
			buffer.WriteD(participant.Points);
			buffer.WriteD(participant.SurvivedTime);
			buffer.WriteQ(participant.ParticipatedEpochSeconds);
			buffer.WriteS(participant.LegionName);
		}
	}

	private static IReadOnlyList<LegionDominionRankEntry> SelectTopParticipants(
		IReadOnlyList<LegionDominionRankEntry> ranking,
		int rank)
	{
		var topParticipants = ranking.Count > 25
			? ranking.Take(25).ToList()
			: ranking.ToList();

		if (rank > topParticipants.Count && topParticipants.Count > 0)
			topParticipants[^1] = ranking[rank - 1];

		return topParticipants;
	}
}
