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

	// Java parity: utils/ChatUtil.ELYOS_NAME_PREFIX = '', ASMO_NAME_PREFIX = '' (private-use client glyphs).
	public const char ElyosNamePrefix = '';
	public const char AsmodianNamePrefix = '';

	public static string ToFactionPrefixedName(bool searcherIsStaff, string candidateRace, string candidateName)
	{
		// Java parity: utils/ChatUtil.toFactionPrefixedName(reader, player).
		// When the searcher (reader) is staff, the candidate's name is prefixed with a faction glyph chosen by the
		// CANDIDATE's race (ELYOS -> ELYOS_NAME_PREFIX, otherwise ASMO_NAME_PREFIX). Non-staff searchers get the plain name.
		// Note: Java's base name is Player.getName(true), which can wrap staff candidates with an admin NAME_TAG
		// (AdminConfig.customtags). The C# port does not model NAME_TAGS anywhere yet, so the plain candidate name is the
		// base here; this matches existing port-wide behavior and is the only documented gap for this helper.
		if (!searcherIsStaff)
			return candidateName;

		var prefix = string.Equals(candidateRace, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? ElyosNamePrefix
			: AsmodianNamePrefix;
		return prefix + candidateName;
	}

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
