namespace Aion.GameServer.Model.Legion;

// Java parity: model/team/legion/LegionRank enum.
// Stored in the legion_members.rank column as the enum NAME (LegionRank.valueOf(getString("rank"))).
// Ordinals (client rank ids): BRIGADE_GENERAL=0, DEPUTY=1, CENTURION=2, LEGIONARY=3, VOLUNTEER=4.
public static class LegionRanks
{
	public const string BrigadeGeneral = "BRIGADE_GENERAL";
	public const string Deputy = "DEPUTY";
	public const string Centurion = "CENTURION";
	public const string Legionary = "LEGIONARY";
	public const string Volunteer = "VOLUNTEER";

	// Java parity: LegionMember default rank = LegionRank.VOLUNTEER.
	public const string Default = Volunteer;

	// Java parity: LegionMember.isBrigadeGeneral() -> rank == LegionRank.BRIGADE_GENERAL.
	public static bool IsBrigadeGeneral(string? rank) => string.Equals(rank, BrigadeGeneral, StringComparison.Ordinal);

	// Java parity: LegionRank.getRankId() -> the byte ordinal assigned in the enum declaration.
	// Returns -1 for an unknown/blank rank (no Java equivalent throws here; callers should treat blank as "no legion").
	public static int GetRankId(string? rank) => rank switch
	{
		BrigadeGeneral => 0,
		Deputy => 1,
		Centurion => 2,
		Legionary => 3,
		Volunteer => 4,
		_ => -1,
	};
}
