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
	private readonly IReadOnlyDictionary<int, IReadOnlyList<SkillLearnSummary>> _templatesBySkillId;

	public SkillTreeTable(IReadOnlyList<SkillLearnSummary> templates, SkillTemplateTable skillTemplates)
	{
		// Java parity: dataholders/SkillTreeData.afterUnmarshal expands blank classId entries to all player classes.
		var templatesByClassRaceLevel = new Dictionary<SkillTreeKey, List<SkillLearnSummary>>();
		var templatesBySkillId = new Dictionary<int, List<SkillLearnSummary>>();
		foreach (var template in templates)
		{
			var skillTemplate = skillTemplates.GetSkillTemplate(template.SkillId);
			var normalized = template with { SkillLevel = skillTemplate?.Level ?? 0 };
			if (string.IsNullOrEmpty(normalized.PlayerClass))
			{
				foreach (var playerClass in PlayerClasses)
					AddTemplate(templatesByClassRaceLevel, templatesBySkillId, normalized with { PlayerClass = playerClass });
			}
			else
			{
				AddTemplate(templatesByClassRaceLevel, templatesBySkillId, normalized);
			}
		}

		_templatesByClassRaceLevel = templatesByClassRaceLevel.ToDictionary(
			pair => pair.Key,
			pair => (IReadOnlyList<SkillLearnSummary>) pair.Value.AsReadOnly());
		_templatesBySkillId = templatesBySkillId.ToDictionary(
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

	public IReadOnlyList<SkillLearnSummary> GetTemplatesForSkill(int skillId, string playerClass, string race)
	{
		// Java parity: dataholders/SkillTreeData.getTemplatesForSkill.
		if (!_templatesBySkillId.TryGetValue(skillId, out var bySkillId))
			return Array.Empty<SkillLearnSummary>();

		return bySkillId
			.Where(template =>
				string.Equals(template.PlayerClass, playerClass, StringComparison.Ordinal)
				&& (string.Equals(template.Race, "PC_ALL", StringComparison.Ordinal)
					|| string.Equals(template.Race, race, StringComparison.Ordinal)))
			.ToArray();
	}

	public IReadOnlyList<SkillLearnSummary> GetSkillsForSkill(
		int skillId,
		string playerClass,
		string race,
		int playerLevel,
		SkillTemplateTable skillTemplates)
	{
		// Java parity: dataholders/SkillTreeData.getSkillsForSkill.
		var skillTree = new List<SkillLearnSummary>();
		foreach (var learnTemplate in GetTemplatesForSkill(GetHighestSkill(skillId, skillTemplates), playerClass, race))
		{
			CreateSkillTree(learnTemplate, skillTree);
			break;
		}

		if (playerLevel > -1)
			skillTree.RemoveAll(template => template.MinLevel > playerLevel);
		return skillTree;
	}

	private void CreateSkillTree(SkillLearnSummary topSkill, List<SkillLearnSummary> addList)
	{
		addList.Insert(0, topSkill);
		if (topSkill.SkillLearn == null)
			return;

		foreach (var template in GetTemplatesForSkill(topSkill.SkillLearn.Value, topSkill.PlayerClass, topSkill.Race))
		{
			if (topSkill.IsStigma != template.IsStigma)
				continue;
			CreateSkillTree(template, addList);
			break;
		}
	}

	private static int GetHighestSkill(int skillId, SkillTemplateTable skillTemplates)
	{
		var baseTemplate = skillTemplates.GetSkillTemplate(skillId);
		if (baseTemplate == null || string.IsNullOrEmpty(baseTemplate.Stack))
			return skillId;

		var stackTemplates = skillTemplates.GetSkillTemplatesByStack(baseTemplate.Stack);
		return stackTemplates.Count == 0
			? skillId
			: stackTemplates.MaxBy(template => template.Level)?.SkillId ?? skillId;
	}

	private static void AddTemplate(
		Dictionary<SkillTreeKey, List<SkillLearnSummary>> templatesByClassRaceLevel,
		Dictionary<int, List<SkillLearnSummary>> templatesBySkillId,
		SkillLearnSummary template)
	{
		var key = new SkillTreeKey(template.PlayerClass, template.Race, template.MinLevel);
		if (!templatesByClassRaceLevel.TryGetValue(key, out var templates))
		{
			templates = [];
			templatesByClassRaceLevel[key] = templates;
		}

		templates.Add(template);

		if (!templatesBySkillId.TryGetValue(template.SkillId, out var bySkillId))
		{
			bySkillId = [];
			templatesBySkillId[template.SkillId] = bySkillId;
		}

		bySkillId.Add(template);
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
	int SkillLevel)
{
	public bool IsStigma => Stigma > 0;

	public bool IsLinkedStigma => Stigma == 4;
}
