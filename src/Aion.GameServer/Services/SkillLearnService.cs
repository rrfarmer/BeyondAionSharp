using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class SkillLearnService
{
	private const int HumanGatheringSkillId = 30001;
	private const int EssenceTappingSkillId = 30002;

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

		if (!ValidateClass(player.PlayerClass.ToString(), action.PlayerClass.ToString()))
			return SkillLearnPlan.Failed(SkillLearnFailure.InvalidClass);

		if (!string.Equals(sourceTemplate.Race, "PC_ALL", StringComparison.Ordinal)
			&& !string.Equals(sourceTemplate.Race.ToString(), player.Race.ToString(), StringComparison.Ordinal))
		{
			return SkillLearnPlan.Failed(SkillLearnFailure.InvalidRace);
		}

		if (player.Skills.Any(skill => skill.SkillId == action.SkillId))
			return SkillLearnPlan.Failed(SkillLearnFailure.AlreadyKnown);

		var finalSkills = player.Skills.ToList();
		var packets = new List<SkillLearnPacket>();
		var learnTemplates = staticData.SkillTree.GetSkillsForSkill(
			action.SkillId,
			player.PlayerClass.ToString(),
			player.Race.ToString(),
			playerLevel,
			staticData.SkillTemplates);
		var matchingTemplates = staticData.SkillTree.GetTemplatesForSkill(action.SkillId, player.PlayerClass.ToString(), player.Race.ToString());
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

	public static SkillAutoLearnPlan CreateAutoLearnPlan(
		Player? player,
		SkillTreeTable? skillTree,
		SkillTemplateTable? skillTemplates,
		int fromLevel,
		int toLevel,
		bool isDaeva,
		bool hasEffectController,
		bool isSpawned)
	{
		if (player == null)
			return SkillAutoLearnPlan.MissingPlayer(fromLevel, toLevel, isDaeva, hasEffectController, isSpawned);
		if (skillTree == null)
			return SkillAutoLearnPlan.Skipped(player, SkillAutoLearnPlanStatus.BlockedMissingSkillTree, fromLevel, toLevel, isDaeva, hasEffectController, isSpawned);
		if (skillTemplates == null)
			return SkillAutoLearnPlan.Skipped(player, SkillAutoLearnPlanStatus.BlockedMissingSkillTemplates, fromLevel, toLevel, isDaeva, hasEffectController, isSpawned);
		if (fromLevel > toLevel)
			return SkillAutoLearnPlan.Skipped(player, SkillAutoLearnPlanStatus.EmptyLevelRange, fromLevel, toLevel, isDaeva, hasEffectController, isSpawned);

		var finalSkills = player.Skills.Select(CloneSkill).ToList();
		var descriptors = new List<SkillAutoLearnDescriptor>();
		var playerClass = player.PlayerClass;
		var startingClass = GetStartingClass(playerClass.ToString());

		for (var level = toLevel; level >= fromLevel; level--)
		{
			if (level < 10 && startingClass != null)
				AddAutoLearnDescriptors(skillTree, skillTemplates, finalSkills, descriptors, player, level, startingClass, hasEffectController, isSpawned);
			AddAutoLearnDescriptors(skillTree, skillTemplates, finalSkills, descriptors, player, level, playerClass, hasEffectController, isSpawned);
		}

		if (toLevel >= 10 && isDaeva && finalSkills.Any(skill => skill.SkillId == HumanGatheringSkillId))
			AddDaevaGatheringUpgrade(skillTemplates, finalSkills, descriptors, player, hasEffectController, isSpawned);

		var applied = descriptors.Any(descriptor => descriptor.Status is
			SkillAutoLearnDescriptorStatus.PlannedAdd or
			SkillAutoLearnDescriptorStatus.PlannedUpgrade or
			SkillAutoLearnDescriptorStatus.PlannedRemove);
		return new SkillAutoLearnPlan(
			applied ? SkillAutoLearnPlanStatus.Planned : SkillAutoLearnPlanStatus.NoChanges,
			player.ObjectId,
			player.PlayerClass.ToString(),
			player.Race.ToString(),
			fromLevel,
			toLevel,
			isDaeva,
			hasEffectController,
			isSpawned,
			finalSkills,
			descriptors);
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

	private static string? GetStartingClass(string playerClass)
	{
		var normalized = playerClass.ToUpperInvariant();
		return StartingClasses.TryGetValue(normalized, out var startingClass) ? startingClass : null;
	}

	private static bool IsStartingClass(string playerClass)
	{
		return !StartingClasses.ContainsKey(playerClass.ToUpperInvariant());
	}

	private static void AddAutoLearnDescriptors(
		SkillTreeTable skillTree,
		SkillTemplateTable skillTemplates,
		List<PlayerSkill> finalSkills,
		List<SkillAutoLearnDescriptor> descriptors,
		Player player,
		int level,
		string learningClass,
		bool hasEffectController,
		bool isSpawned)
	{
		foreach (var template in skillTree.GetTemplatesFor(learningClass, level, player.Race.ToString()))
		{
			if (!template.AutoLearn)
			{
				descriptors.Add(SkillAutoLearnDescriptor.Skipped(
					template,
					level,
					learningClass,
					SkillAutoLearnDescriptorStatus.SkippedNotAutoLearn,
					"SkillLearnService.autoLearnSkills -> !template.isAutolearn()"));
				continue;
			}

			if (template.SkillId == HumanGatheringSkillId && !IsStartingClass(learningClass))
			{
				descriptors.Add(SkillAutoLearnDescriptor.Skipped(
					template,
					level,
					learningClass,
					SkillAutoLearnDescriptorStatus.SkippedHumanGatheringForAdvancedClass,
					"SkillLearnService.autoLearnSkills -> no human gathering for main classes"));
				continue;
			}

			var matchingTemplates = skillTree.GetTemplatesForSkill(template.SkillId, player.PlayerClass.ToString(), player.Race.ToString());
			var learnTemplates = skillTree.GetSkillsForSkill(template.SkillId, player.PlayerClass.ToString(), player.Race.ToString(), player.Level, skillTemplates);
			var skillType = ResolveSkillType(template.SkillId, matchingTemplates, skillTemplates);
			var packet = AddOrUpgradeSkill(finalSkills, template.SkillId, template.SkillLevel, skillType, learnTemplates);
			if (packet == null)
			{
				descriptors.Add(SkillAutoLearnDescriptor.Skipped(
					template,
					level,
					learningClass,
					SkillAutoLearnDescriptorStatus.SkippedAlreadyKnownAtSameOrHigherLevel,
					"PlayerSkillList.addSkill -> skillLevel <= existingSkill.getSkillLevel()"));
				continue;
			}

			var learnedSkill = finalSkills.Single(skill => skill.SkillId == template.SkillId);
			descriptors.Add(SkillAutoLearnDescriptor.Planned(
				template,
				level,
				learningClass,
				packet.IsNew ? SkillAutoLearnDescriptorStatus.PlannedAdd : SkillAutoLearnDescriptorStatus.PlannedUpgrade,
				learnedSkill,
				packet,
				CreateSideEffects(learnedSkill, packet.IsNew, skillTemplates, hasEffectController, isSpawned)));
		}
	}

	private static void AddDaevaGatheringUpgrade(
		SkillTemplateTable skillTemplates,
		List<PlayerSkill> finalSkills,
		List<SkillAutoLearnDescriptor> descriptors,
		Player player,
		bool hasEffectController,
		bool isSpawned)
	{
		var humanGathering = finalSkills.Single(skill => skill.SkillId == HumanGatheringSkillId);
		SkillLearnPacket? packet = null;
		if (finalSkills.All(skill => skill.SkillId != EssenceTappingSkillId))
		{
			var essenceTapping = new PlayerSkill
			{
				SkillId = EssenceTappingSkillId,
				SkillLevel = humanGathering.SkillLevel,
				SkillType = humanGathering.SkillType,
			};
			finalSkills.Add(essenceTapping);
			packet = new SkillLearnPacket(essenceTapping, IsNew: true);
			descriptors.Add(new SkillAutoLearnDescriptor(
				EssenceTappingSkillId,
				humanGathering.SkillLevel,
				player.Level,
				player.PlayerClass.ToString(),
				SkillAutoLearnDescriptorStatus.PlannedAdd,
				"SkillLearnService.learnNewSkills -> upgrade human gathering to daeva essence tapping",
				essenceTapping,
				packet,
				CreateSideEffects(essenceTapping, isNew: true, skillTemplates, hasEffectController, isSpawned),
				Notes: "Java adds skill 30002 at the current human gathering level before removing skill 30001."));
		}

		finalSkills.RemoveAll(skill => skill.SkillId == HumanGatheringSkillId);
		descriptors.Add(new SkillAutoLearnDescriptor(
			HumanGatheringSkillId,
			humanGathering.SkillLevel,
			player.Level,
			player.PlayerClass.ToString(),
			SkillAutoLearnDescriptorStatus.PlannedRemove,
			"SkillLearnService.learnNewSkills -> removeSkill(30001)",
			RemovedSkill: humanGathering,
			Packet: null,
			SideEffects: [SkillAutoLearnSideEffect.RemoveEffect, SkillAutoLearnSideEffect.SkillRemovePacket],
			Notes: packet == null
				? "Java removes human gathering even when essence tapping was already present."
				: "Java removes human gathering after adding essence tapping."));
	}

	private static IReadOnlyList<SkillAutoLearnSideEffect> CreateSideEffects(
		PlayerSkill skill,
		bool isNew,
		SkillTemplateTable skillTemplates,
		bool hasEffectController,
		bool isSpawned)
	{
		var sideEffects = new List<SkillAutoLearnSideEffect>();
		if (skill.IsProfessionSkill && skill.SkillLevel is 1 or 100 or 200 or 300 or 400 or 450 or 500
			&& (skill.SkillLevel != 1 || skill.IsCraftingSkill))
		{
			sideEffects.Add(SkillAutoLearnSideEffect.CraftLevelUpAnimationBroadcast);
		}

		if (hasEffectController)
		{
			if (isSpawned)
				sideEffects.Add(SkillAutoLearnSideEffect.SkillListPacket);
			if (skillTemplates.GetSkillTemplate(skill.SkillId)?.IsPassive == true)
				sideEffects.Add(SkillAutoLearnSideEffect.ApplyPassiveEffect);
			if (skill.IsProfessionSkill && skill.SkillLevel is 399 or 499)
				sideEffects.Add(SkillAutoLearnSideEffect.NearbyQuestRefresh);
		}

		if (skill.IsCraftingSkill || skill.IsMorphSkill)
			sideEffects.Add(SkillAutoLearnSideEffect.AutoLearnRecipes);
		return sideEffects;
	}

	private static PlayerSkill CloneSkill(PlayerSkill skill)
	{
		return new PlayerSkill
		{
			SkillId = skill.SkillId,
			SkillLevel = skill.SkillLevel,
			SkillType = skill.SkillType,
			CurrentXp = skill.CurrentXp,
		};
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

public sealed record SkillAutoLearnPlan(
	SkillAutoLearnPlanStatus Status,
	int ObjectId,
	string PlayerClass,
	string Race,
	int FromLevel,
	int ToLevel,
	bool IsDaeva,
	bool HasEffectController,
	bool IsSpawned,
	IReadOnlyList<PlayerSkill> FinalSkills,
	IReadOnlyList<SkillAutoLearnDescriptor> Descriptors)
{
	public bool Applied => Status == SkillAutoLearnPlanStatus.Planned;

	public static SkillAutoLearnPlan MissingPlayer(
		int fromLevel,
		int toLevel,
		bool isDaeva,
		bool hasEffectController,
		bool isSpawned)
	{
		return new SkillAutoLearnPlan(
			SkillAutoLearnPlanStatus.MissingPlayer,
			ObjectId: 0,
			PlayerClass: string.Empty,
			Race: string.Empty,
			fromLevel,
			toLevel,
			isDaeva,
			hasEffectController,
			isSpawned,
			Array.Empty<PlayerSkill>(),
			Array.Empty<SkillAutoLearnDescriptor>());
	}

	public static SkillAutoLearnPlan Skipped(
		Player player,
		SkillAutoLearnPlanStatus status,
		int fromLevel,
		int toLevel,
		bool isDaeva,
		bool hasEffectController,
		bool isSpawned)
	{
		return new SkillAutoLearnPlan(
			status,
			player.ObjectId,
			player.PlayerClass.ToString(),
			player.Race.ToString(),
			fromLevel,
			toLevel,
			isDaeva,
			hasEffectController,
			isSpawned,
			player.Skills,
			Array.Empty<SkillAutoLearnDescriptor>());
	}
}

public sealed record SkillAutoLearnDescriptor(
	int SkillId,
	int SkillLevel,
	int Level,
	string LearningClass,
	SkillAutoLearnDescriptorStatus Status,
	string JavaSource,
	PlayerSkill? LearnedSkill = null,
	SkillLearnPacket? Packet = null,
	IReadOnlyList<SkillAutoLearnSideEffect>? SideEffects = null,
	PlayerSkill? RemovedSkill = null,
	string? Notes = null)
{
	public bool IsLive => false;

	public IReadOnlyList<SkillAutoLearnSideEffect> PlannedSideEffects => SideEffects ?? Array.Empty<SkillAutoLearnSideEffect>();

	public static SkillAutoLearnDescriptor Planned(
		SkillLearnSummary template,
		int level,
		string learningClass,
		SkillAutoLearnDescriptorStatus status,
		PlayerSkill learnedSkill,
		SkillLearnPacket packet,
		IReadOnlyList<SkillAutoLearnSideEffect> sideEffects)
	{
		return new SkillAutoLearnDescriptor(
			template.SkillId,
			template.SkillLevel,
			level,
			learningClass,
			status,
			"SkillLearnService.learnNewSkills -> autoLearnSkills -> PlayerSkillList.addSkill -> SkillLearnService.onLearnSkill",
			learnedSkill,
			packet,
			sideEffects);
	}

	public static SkillAutoLearnDescriptor Skipped(
		SkillLearnSummary template,
		int level,
		string learningClass,
		SkillAutoLearnDescriptorStatus status,
		string javaSource)
	{
		return new SkillAutoLearnDescriptor(
			template.SkillId,
			template.SkillLevel,
			level,
			learningClass,
			status,
			javaSource);
	}
}

public enum SkillAutoLearnPlanStatus
{
	Planned,
	NoChanges,
	EmptyLevelRange,
	BlockedMissingSkillTree,
	BlockedMissingSkillTemplates,
	MissingPlayer,
}

public enum SkillAutoLearnDescriptorStatus
{
	PlannedAdd,
	PlannedUpgrade,
	PlannedRemove,
	SkippedNotAutoLearn,
	SkippedHumanGatheringForAdvancedClass,
	SkippedAlreadyKnownAtSameOrHigherLevel,
}

public enum SkillAutoLearnSideEffect
{
	SkillListPacket,
	SkillRemovePacket,
	CraftLevelUpAnimationBroadcast,
	ApplyPassiveEffect,
	NearbyQuestRefresh,
	AutoLearnRecipes,
	RemoveEffect,
}
