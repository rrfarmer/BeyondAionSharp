namespace Aion.GameServer.Services;

// Java parity: network/aion/clientpackets/CM_PLAYER_SEARCH.runImpl candidate filter.
public sealed record PlayerSearchCriteria(
	string SearcherRace,
	bool SearcherIsStaff,
	string NameFilter,
	int Region,
	int ClassMask,
	int MinLevel, // 0xFF (255) means unset
	int MaxLevel, // 0xFF (255) means unset
	int LfgOnly,
	bool FactionsSearchMode,
	bool SearchGmList);

public sealed record PlayerSearchCandidate(
	int ObjectId,
	string Name,
	string Race,
	int Level,
	int ClassId,
	int WorldId,
	bool IsStaff,
	bool IsLookingForGroup,
	bool FriendStatusOffline);

public static class PlayerSearchMatchService
{
	public const int MaxResults = 104; // Java parity: CM_PLAYER_SEARCH.MAX_RESULTS (3.0).
	private const int LevelUnset = 0xFF;

	public static bool Matches(PlayerSearchCriteria criteria, PlayerSearchCandidate candidate, int searcherObjectId)
	{
		// Java parity: CM_PLAYER_SEARCH.runImpl per-player filter loop.
		if (!criteria.SearcherIsStaff)
		{
			// Non-staff searchers: race-gated (unless factions search), no appear-offline, no staff (unless gm list).
			if (!string.Equals(candidate.Race, criteria.SearcherRace, StringComparison.OrdinalIgnoreCase)
				&& !criteria.FactionsSearchMode)
				return false;
			if (candidate.FriendStatusOffline)
				return false;
			if (candidate.IsStaff && !criteria.SearchGmList)
				return false;
		}

		if (criteria.LfgOnly == 1 && !candidate.IsLookingForGroup)
			return false;

		if (!string.IsNullOrEmpty(criteria.NameFilter)
			&& !candidate.Name.Contains(criteria.NameFilter, StringComparison.OrdinalIgnoreCase))
			return false;

		if (criteria.MinLevel != LevelUnset && candidate.Level < criteria.MinLevel)
			return false;

		if (criteria.MaxLevel != LevelUnset && candidate.Level > criteria.MaxLevel)
			return false;

		if (criteria.ClassMask > 0 && ((1 << candidate.ClassId) & criteria.ClassMask) == 0)
			return false;

		if (criteria.Region > 0 && candidate.WorldId != criteria.Region)
			return false;

		if (candidate.ObjectId == searcherObjectId)
			return false;

		return true;
	}
}
