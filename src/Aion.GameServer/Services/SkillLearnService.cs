using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class SkillLearnService
{
	private static readonly IReadOnlyDictionary<string, string> StartingClasses = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["GLADIATOR"] = "WARRIOR",
		["TEMPLAR"] = "WARRIOR",
		["ASSASSIN"] = "SCOUT",
		["RANGER"] = "SCOUT",
		["SORCERER"] = "MAGE",
		["SPIRIT_MASTER"] = "MAGE",
		["CLERIC"] = "PRIEST",
		["CHANTER"] = "PRIEST",
		["RIDER"] = "ENGINEER",
		["GUNNER"] = "ENGINEER",
		["BARD"] = "ARTIST",
	};

	public static SkillLearnPlan CreateSkillBookPlan(
		Player player,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData)
	{
		// Java parity: model/templates/item/actions/SkillLearnAction.canAct + services/SkillLearnService.learnSkillBook.
		var action = sourceTemplate.SkillLearnAction;
		if (action == null)
			return SkillLearnPlan.Failed(SkillLearnFailure.MissingAction);

		var playerLevel = Math.Max(1, staticData.PlayerExperienceTable.GetLevelForExp(player.Exp));
		if (playerLevel < action.Level)
			return SkillLearnPlan.Failed(SkillLearnFailure.TooLowLevel);

		if (!ValidateClass(player.PlayerClass, action.PlayerClass))
			return SkillLearnPlan.Failed(SkillLearnFailure.InvalidClass);

		if (!string.Equals(sourceTemplate.Race, "PC_ALL", StringComparison.Ordinal)
			&& !string.Equals(sourceTemplate.Race, player.Race, StringComparison.Ordinal))
		{
			return SkillLearnPlan.Failed(SkillLearnFailure.InvalidRace);
		}

		if (player.Skills.Any(skill => skill.SkillId == action.SkillId))
			return SkillLearnPlan.Failed(SkillLearnFailure.AlreadyKnown);

		var finalSkills = player.Skills.ToList();
		var packets = new List<SkillLearnPacket>();
		var learnTemplates = staticData.SkillTree.GetSkillsForSkill(
			action.SkillId,
			player.PlayerClass,
			player.Race,
			playerLevel,
			staticData.SkillTemplates);
		var matchingTemplates = staticData.SkillTree.GetTemplatesForSkill(action.SkillId, player.PlayerClass, player.Race);
		var skillType = ResolveSkillType(action.SkillId, matchingTemplates, staticData.SkillTemplates);
		foreach (var learnTemplate in learnTemplates)
		{
			var packet = AddOrUpgradeSkill(
				finalSkills,
				action.SkillId,
				learnTemplate.SkillLevel,
				skillType,
				learnTemplates);
			if (packet != null)
				packets.Add(packet);
		}

		return new SkillLearnPlan(SkillLearnFailure.None, finalSkills, packets);
	}

	private static bool ValidateClass(string playerClass, string actionClass)
	{
		if (string.IsNullOrEmpty(actionClass))
			return true;

		var normalizedPlayerClass = playerClass.ToUpperInvariant();
		var normalizedActionClass = actionClass.ToUpperInvariant();
		return string.Equals(normalizedActionClass, normalizedPlayerClass, StringComparison.Ordinal)
			|| (StartingClasses.TryGetValue(normalizedPlayerClass, out var startingClass)
				&& string.Equals(normalizedActionClass, startingClass, StringComparison.Ordinal));
	}

	private static int ResolveSkillType(
		int skillId,
		IReadOnlyList<SkillLearnSummary> matchingTemplates,
		SkillTemplateTable skillTemplates)
	{
		foreach (var template in matchingTemplates)
		{
			if (template.IsLinkedStigma)
				return 3;
			if (template.IsStigma)
				return 1;
		}

		return skillTemplates.GetSkillTemplate(skillId)?.IsStigmaSkill == true ? 1 : 0;
	}

	private static SkillLearnPacket? AddOrUpgradeSkill(
		List<PlayerSkill> skills,
		int skillId,
		int skillLevel,
		int skillType,
		IReadOnlyList<SkillLearnSummary> learnTemplates)
	{
		var index = skills.FindIndex(skill => skill.SkillId == skillId);
		if (index >= 0)
		{
			var existing = skills[index];
			if (skillLevel <= existing.SkillLevel)
				return null;

			var upgraded = new PlayerSkill
			{
				SkillId = skillId,
				SkillLevel = skillLevel,
				SkillType = existing.SkillType,
				CurrentXp = existing.CurrentXp,
			};
			skills[index] = upgraded;
			return new SkillLearnPacket(upgraded, IsNew: false);
		}

		var isNew = !learnTemplates.Any(template =>
			template.SkillLearn.HasValue
			&& skills.Any(skill => skill.SkillId == template.SkillLearn.Value));
		var added = new PlayerSkill
		{
			SkillId = skillId,
			SkillLevel = skillLevel,
			SkillType = skillType,
		};
		skills.Add(added);
		return new SkillLearnPacket(added, isNew);
	}
}

public sealed record SkillLearnPlan(
	SkillLearnFailure Failure,
	IReadOnlyList<PlayerSkill> Skills,
	IReadOnlyList<SkillLearnPacket> Packets)
{
	public bool Succeeded => Failure == SkillLearnFailure.None;

	public IReadOnlyList<PlayerSkill> PersistedSkills => Packets.Select(packet => packet.Skill).ToArray();

	public static SkillLearnPlan Failed(SkillLearnFailure failure)
	{
		return new SkillLearnPlan(failure, Array.Empty<PlayerSkill>(), Array.Empty<SkillLearnPacket>());
	}
}

public sealed record SkillLearnPacket(PlayerSkill Skill, bool IsNew)
{
	public int MessageId => SkillLearnServiceMessages.GetMessageId(Skill, IsNew);
}

public static class SkillLearnServiceMessages
{
	public static int GetMessageId(PlayerSkill skill, bool isNew)
	{
		// Java parity: services/SkillLearnService.sendPacket.
		if (skill.IsProfessionSkill)
		{
			if (skill.IsTappingSkill)
				return isNew ? 1330004 : 1330005;
			return isNew ? 1330061 : 1330064;
		}

		if (!isNew)
			return 0;

		return skill.IsStigmaSkill
			? skill.SkillType >= 3 ? 1402891 : 1300401
			: 1300050;
	}
}

public enum SkillLearnFailure
{
	None,
	MissingAction,
	TooLowLevel,
	InvalidClass,
	InvalidRace,
	AlreadyKnown,
}
