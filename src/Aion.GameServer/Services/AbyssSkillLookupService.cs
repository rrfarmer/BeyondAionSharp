namespace Aion.GameServer.Services;

public sealed record AbyssSkillLookupPlan(
	string Race,
	int RankId,
	IReadOnlyList<int> SkillIds,
	bool HasAbyssSkills,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class AbyssSkillLookupService
{
	// Java parity: services/abyss/AbyssSkillService.AbyssSkills enum.
	// Maps (race, rankId) -> skill IDs. Only STAR5_OFFICER (14) through SUPREME_COMMANDER (18) have abyss skills.
	public const int MinAbyssSkillRankId = 14; // STAR5_OFFICER

	private static readonly IReadOnlyDictionary<(string Race, int RankId), int[]> SkillsByRaceAndRank =
		new Dictionary<(string, int), int[]>
		{
			// ELYOS
			[("ELYOS", 14)] = [11885, 11895],                              // STAR5_OFFICER
			[("ELYOS", 15)] = [11886, 11896, 11899],                       // GENERAL
			[("ELYOS", 16)] = [11887, 11897, 11899, 11903],               // GREAT_GENERAL
			[("ELYOS", 17)] = [11888, 11898, 11900, 11903, 11904],        // COMMANDER
			[("ELYOS", 18)] = [11889, 11898, 11900, 11903, 11904, 11905, 11906], // SUPREME_COMMANDER
			// ASMODIANS
			[("ASMODIANS", 14)] = [11890, 11895],                          // STAR5_OFFICER
			[("ASMODIANS", 15)] = [11891, 11896, 11901],                   // GENERAL
			[("ASMODIANS", 16)] = [11892, 11897, 11901, 11903],           // GREAT_GENERAL
			[("ASMODIANS", 17)] = [11893, 11898, 11902, 11903, 11904],    // COMMANDER
			[("ASMODIANS", 18)] = [11894, 11898, 11902, 11903, 11904, 11905, 11906], // SUPREME_COMMANDER
		};

	public static AbyssSkillLookupPlan CreatePlan(string race, int rankId)
	{
		// Java parity: services/abyss/AbyssSkillService.AbyssSkills.getSkills(Race, AbyssRankEnum).
		var raceUpper = race.ToUpperInvariant();
		if (SkillsByRaceAndRank.TryGetValue((raceUpper, rankId), out var skills))
		{
			return new AbyssSkillLookupPlan(
				raceUpper, rankId, skills, HasAbyssSkills: true,
				$"AbyssSkillService -> {raceUpper} rank {rankId} -> {skills.Length} abyss skill(s)"
			);
		}

		return new AbyssSkillLookupPlan(
			raceUpper, rankId, Array.Empty<int>(), HasAbyssSkills: false,
			$"AbyssSkillService -> {raceUpper} rank {rankId} below STAR5_OFFICER or not found -> no abyss skills"
		);
	}
}
