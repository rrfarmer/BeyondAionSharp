namespace Aion.GameServer.Dataholders;

public sealed class SkillTreeTable
{
	private static readonly string[] PlayerClasses =
	[
		"WARRIOR",
		"GLADIATOR",
		"TEMPLAR",
		"SCOUT",
		"ASSASSIN",
		"RANGER",
		"MAGE",
		"SORCERER",
		"SPIRIT_MASTER",
		"PRIEST",
		"CLERIC",
		"CHANTER",
		"ENGINEER",
		"RIDER",
		"GUNNER",
		"ARTIST",
		"BARD",
	];

	private readonly IReadOnlyDictionary<SkillTreeKey, IReadOnlyList<SkillLearnSummary>> _templatesByClassRaceLevel;

	public SkillTreeTable(IReadOnlyList<SkillLearnSummary> templates, SkillTemplateTable skillTemplates)
	{
		// Java parity: dataholders/SkillTreeData.afterUnmarshal expands blank classId entries to all player classes.
		var templatesByClassRaceLevel = new Dictionary<SkillTreeKey, List<SkillLearnSummary>>();
		foreach (var template in templates)
		{
			var skillTemplate = skillTemplates.GetSkillTemplate(template.SkillId);
			var normalized = template with { SkillLevel = skillTemplate?.Level ?? 0 };
			if (string.IsNullOrEmpty(normalized.PlayerClass))
			{
				foreach (var playerClass in PlayerClasses)
					AddTemplate(templatesByClassRaceLevel, normalized with { PlayerClass = playerClass });
			}
			else
			{
				AddTemplate(templatesByClassRaceLevel, normalized);
			}
		}

		_templatesByClassRaceLevel = templatesByClassRaceLevel.ToDictionary(
			pair => pair.Key,
			pair => (IReadOnlyList<SkillLearnSummary>) pair.Value.AsReadOnly());
	}

	public int Count => _templatesByClassRaceLevel.Values.Sum(templates => templates.Count);

	public IReadOnlyList<SkillLearnSummary> GetTemplatesFor(string playerClass, int level, string race)
	{
		// Java parity: dataholders/SkillTreeData.getTemplatesFor(playerClass, level, race).
		var result = new List<SkillLearnSummary>();
		if (_templatesByClassRaceLevel.TryGetValue(new SkillTreeKey(playerClass, race, level), out var raceSpecific))
			result.AddRange(raceSpecific);
		if (_templatesByClassRaceLevel.TryGetValue(new SkillTreeKey(playerClass, "PC_ALL", level), out var classSpecific))
			result.AddRange(classSpecific);

		return result;
	}

	public IReadOnlyList<SkillLearnSummary> GetAutoLearnSkills(string playerClass, string race, int fromLevel, int toLevel)
	{
		// Java parity: services/SkillLearnService.learnNewSkills level loop.
		var result = new List<SkillLearnSummary>();
		for (var level = toLevel; level >= fromLevel; level--)
			result.AddRange(GetTemplatesFor(playerClass, level, race).Where(template => template.AutoLearn));

		return result;
	}

	private static void AddTemplate(Dictionary<SkillTreeKey, List<SkillLearnSummary>> templatesByClassRaceLevel, SkillLearnSummary template)
	{
		var key = new SkillTreeKey(template.PlayerClass, template.Race, template.MinLevel);
		if (!templatesByClassRaceLevel.TryGetValue(key, out var templates))
		{
			templates = [];
			templatesByClassRaceLevel[key] = templates;
		}

		templates.Add(template);
	}

	private sealed record SkillTreeKey(string PlayerClass, string Race, int Level);
}

public sealed record SkillLearnSummary(
	string PlayerClass,
	int SkillId,
	int? SkillLearn,
	string Race,
	int MinLevel,
	bool AutoLearn,
	int Stigma,
	int SkillLevel);
