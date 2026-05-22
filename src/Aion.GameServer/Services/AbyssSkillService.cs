using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class AbyssSkillService
{
	public const int DefaultTransformMinRank = 14;

	private static readonly IReadOnlyDictionary<int, int[]> ElyosSkillsByRank = new Dictionary<int, int[]>
	{
		[18] = [11889, 11898, 11900, 11903, 11904, 11905, 11906],
		[17] = [11888, 11898, 11900, 11903, 11904],
		[16] = [11887, 11897, 11899, 11903],
		[15] = [11886, 11896, 11899],
		[14] = [11885, 11895],
	};

	private static readonly IReadOnlyDictionary<int, int[]> AsmodianSkillsByRank = new Dictionary<int, int[]>
	{
		[18] = [11894, 11898, 11902, 11903, 11904, 11905, 11906],
		[17] = [11893, 11898, 11902, 11903, 11904],
		[16] = [11892, 11897, 11901, 11903],
		[15] = [11891, 11896, 11901],
		[14] = [11890, 11895],
	};

	public static AbyssSkillUpdateResult UpdateSkills(Player player, int transformMinRank = DefaultTransformMinRank)
	{
		// Java parity: services/abyss/AbyssSkillService.updateSkills.
		var skills = player.Skills.ToList();
		var removedSkills = new List<PlayerSkill>();
		var addedSkills = new List<PlayerSkill>();

		foreach (var skillId in GetAllAbyssSkillIds(player.Race))
		{
			var removed = RemoveSkill(skills, skillId);
			if (removed != null)
				removedSkills.Add(removed);
		}

		if (player.AbyssRank.Rank >= transformMinRank)
		{
			foreach (var skillId in GetSkills(player.Race, player.AbyssRank.Rank))
			{
				var added = AddTemporarySkill(skills, skillId, skillLevel: 1);
				if (added != null)
					addedSkills.Add(added);
			}
		}

		return new AbyssSkillUpdateResult(skills, addedSkills, removedSkills);
	}

	private static IEnumerable<int> GetAllAbyssSkillIds(string race)
	{
		return GetSkillMap(race).Values.SelectMany(skillIds => skillIds).Distinct();
	}

	private static IReadOnlyList<int> GetSkills(string race, int rank)
	{
		return GetSkillMap(race).GetValueOrDefault(rank) ?? Array.Empty<int>();
	}

	private static IReadOnlyDictionary<int, int[]> GetSkillMap(string race)
	{
		return string.Equals(race, "ASMODIANS", StringComparison.Ordinal)
			? AsmodianSkillsByRank
			: ElyosSkillsByRank;
	}

	private static PlayerSkill? AddTemporarySkill(List<PlayerSkill> skills, int skillId, int skillLevel)
	{
		// Java parity: services/SkillLearnService.learnTemporarySkill.
		var existingIndex = skills.FindIndex(skill => skill.SkillId == skillId);
		if (existingIndex >= 0)
		{
			var existing = skills[existingIndex];
			if (skillLevel <= existing.SkillLevel)
				return null;

			var upgraded = new PlayerSkill
			{
				SkillId = existing.SkillId,
				SkillLevel = skillLevel,
				SkillType = existing.SkillType,
				CurrentXp = existing.CurrentXp,
			};
			skills[existingIndex] = upgraded;
			return upgraded;
		}

		var added = new PlayerSkill { SkillId = skillId, SkillLevel = skillLevel, SkillType = 0 };
		skills.Add(added);
		return added;
	}

	private static PlayerSkill? RemoveSkill(List<PlayerSkill> skills, int skillId)
	{
		// Java parity: services/SkillLearnService.removeSkill.
		var existingIndex = skills.FindIndex(skill => skill.SkillId == skillId);
		if (existingIndex < 0)
			return null;

		var removed = skills[existingIndex];
		skills.RemoveAt(existingIndex);
		return removed;
	}
}

public sealed record AbyssSkillUpdateResult(
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<PlayerSkill> AddedSkills,
	IReadOnlyList<PlayerSkill> RemovedSkills)
{
	public bool Changed => AddedSkills.Count > 0 || RemovedSkills.Count > 0;
}
