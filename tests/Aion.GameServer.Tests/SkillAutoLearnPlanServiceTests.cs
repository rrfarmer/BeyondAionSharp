using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillAutoLearnPlanServiceTests
{
	[Fact]
	public void CreateAutoLearnPlan_StagesJavaReverseLevelLoopStartingClassBackfillAndDaevaGatheringUpgrade()
	{
		var player = new Player
		{
			ObjectId = 4601,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Skills = [new PlayerSkill { SkillId = 201, SkillLevel = 1 }],
		};
		var skillTemplates = CreateSkillTemplates(
			Skill(201, level: 2, activation: "PASSIVE"),
			Skill(301, level: 1),
			Skill(30001, level: 37),
			Skill(30002, level: 37),
			Skill(40001, level: 1));
		var skillTree = CreateSkillTree(
			skillTemplates,
			new SkillLearnSummary("RANGER", 201, null, "ELYOS", 10, AutoLearn: true, Stigma: 0, SkillLevel: 0),
			new SkillLearnSummary("RANGER", 202, null, "ELYOS", 10, AutoLearn: false, Stigma: 0, SkillLevel: 0),
			new SkillLearnSummary("SCOUT", 30001, null, "ELYOS", 9, AutoLearn: true, Stigma: 0, SkillLevel: 0),
			new SkillLearnSummary("RANGER", 30001, null, "ELYOS", 9, AutoLearn: true, Stigma: 0, SkillLevel: 0),
			new SkillLearnSummary("SCOUT", 301, null, "PC_ALL", 8, AutoLearn: true, Stigma: 0, SkillLevel: 0),
			new SkillLearnSummary("RANGER", 40001, null, "ELYOS", 8, AutoLearn: true, Stigma: 0, SkillLevel: 0));

		var plan = SkillLearnService.CreateAutoLearnPlan(
			player,
			skillTree,
			skillTemplates,
			fromLevel: 8,
			toLevel: 10,
			isDaeva: true,
			hasEffectController: true,
			isSpawned: true);

		Assert.Equal(SkillAutoLearnPlanStatus.Planned, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(
		[
			(201, 10, "RANGER", SkillAutoLearnDescriptorStatus.PlannedUpgrade),
			(202, 10, "RANGER", SkillAutoLearnDescriptorStatus.SkippedNotAutoLearn),
			(30001, 9, "SCOUT", SkillAutoLearnDescriptorStatus.PlannedAdd),
			(30001, 9, "RANGER", SkillAutoLearnDescriptorStatus.SkippedHumanGatheringForAdvancedClass),
			(301, 8, "SCOUT", SkillAutoLearnDescriptorStatus.PlannedAdd),
			(40001, 8, "RANGER", SkillAutoLearnDescriptorStatus.PlannedAdd),
			(30002, 10, "RANGER", SkillAutoLearnDescriptorStatus.PlannedAdd),
			(30001, 10, "RANGER", SkillAutoLearnDescriptorStatus.PlannedRemove),
		], plan.Descriptors.Select(descriptor => (descriptor.SkillId, descriptor.Level, descriptor.LearningClass, descriptor.Status)));
		Assert.DoesNotContain(plan.FinalSkills, skill => skill.SkillId == 30001);
		Assert.Contains(plan.FinalSkills, skill => skill.SkillId == 30002 && skill.SkillLevel == 37);
		Assert.Contains(plan.FinalSkills, skill => skill.SkillId == 201 && skill.SkillLevel == 2);
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.SkillId == 201
				&& descriptor.PlannedSideEffects.Contains(SkillAutoLearnSideEffect.SkillListPacket)
				&& descriptor.PlannedSideEffects.Contains(SkillAutoLearnSideEffect.ApplyPassiveEffect)
				&& descriptor.Packet is { IsNew: false, MessageId: 0 });
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.SkillId == 40001
				&& descriptor.PlannedSideEffects.Contains(SkillAutoLearnSideEffect.CraftLevelUpAnimationBroadcast)
				&& descriptor.PlannedSideEffects.Contains(SkillAutoLearnSideEffect.AutoLearnRecipes)
				&& descriptor.Packet is { IsNew: true, MessageId: 1330061 });
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.SkillId == 30001
				&& descriptor.Status == SkillAutoLearnDescriptorStatus.PlannedRemove
				&& descriptor.PlannedSideEffects.SequenceEqual(
				[
					SkillAutoLearnSideEffect.RemoveEffect,
					SkillAutoLearnSideEffect.SkillRemovePacket,
				]));
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal([201, 30001, 301, 40001, 30002], plan.Descriptors.Where(descriptor => descriptor.Packet != null).Select(descriptor => descriptor.SkillId));
	}

	[Fact]
	public void CreateAutoLearnPlan_RecordsMissingInputsNoChangesAndAlreadyKnownBranches()
	{
		var player = new Player
		{
			ObjectId = 4602,
			Race = "ELYOS",
			PlayerClass = "SCOUT",
			Level = 9,
			Skills = [new PlayerSkill { SkillId = 301, SkillLevel = 2 }],
		};
		var skillTemplates = CreateSkillTemplates(Skill(301, level: 2), Skill(302, level: 1));
		var skillTree = CreateSkillTree(
			skillTemplates,
			new SkillLearnSummary("SCOUT", 301, null, "ELYOS", 9, AutoLearn: true, Stigma: 0, SkillLevel: 0),
			new SkillLearnSummary("SCOUT", 302, null, "ASMODIANS", 9, AutoLearn: true, Stigma: 0, SkillLevel: 0));

		var alreadyKnown = SkillLearnService.CreateAutoLearnPlan(player, skillTree, skillTemplates, 9, 9, isDaeva: false, hasEffectController: false, isSpawned: false);
		var emptyRange = SkillLearnService.CreateAutoLearnPlan(player, skillTree, skillTemplates, 10, 9, isDaeva: false, hasEffectController: false, isSpawned: false);
		var missingTree = SkillLearnService.CreateAutoLearnPlan(player, null, skillTemplates, 9, 9, isDaeva: false, hasEffectController: false, isSpawned: false);
		var missingTemplates = SkillLearnService.CreateAutoLearnPlan(player, skillTree, null, 9, 9, isDaeva: false, hasEffectController: false, isSpawned: false);
		var missingPlayer = SkillLearnService.CreateAutoLearnPlan(null, skillTree, skillTemplates, 9, 9, isDaeva: false, hasEffectController: false, isSpawned: false);

		Assert.Equal(SkillAutoLearnPlanStatus.NoChanges, alreadyKnown.Status);
		Assert.False(alreadyKnown.Applied);
		var descriptor = Assert.Single(alreadyKnown.Descriptors);
		Assert.Equal(SkillAutoLearnDescriptorStatus.SkippedAlreadyKnownAtSameOrHigherLevel, descriptor.Status);
		Assert.Empty(descriptor.PlannedSideEffects);
		Assert.Equal(SkillAutoLearnPlanStatus.EmptyLevelRange, emptyRange.Status);
		Assert.Equal(SkillAutoLearnPlanStatus.BlockedMissingSkillTree, missingTree.Status);
		Assert.Equal(SkillAutoLearnPlanStatus.BlockedMissingSkillTemplates, missingTemplates.Status);
		Assert.Equal(SkillAutoLearnPlanStatus.MissingPlayer, missingPlayer.Status);
		Assert.Empty(emptyRange.Descriptors);
		Assert.Empty(missingTree.Descriptors);
		Assert.Empty(missingTemplates.Descriptors);
		Assert.Empty(missingPlayer.Descriptors);
	}

	private static SkillTemplateTable CreateSkillTemplates(params SkillTemplateSummary[] templates)
	{
		return new SkillTemplateTable(templates);
	}

	private static SkillTreeTable CreateSkillTree(SkillTemplateTable skillTemplates, params SkillLearnSummary[] templates)
	{
		return new SkillTreeTable(templates, skillTemplates);
	}

	private static SkillTemplateSummary Skill(int skillId, int level, string activation = "")
	{
		return new SkillTemplateSummary(
			skillId,
			Name: $"skill_{skillId}",
			NameId: 0,
			level,
			Group: string.Empty,
			Stack: string.Empty,
			SkillType: string.Empty,
			SkillSubType: string.Empty,
			CooldownId: 0,
			Cooldown: 0,
			Activation: activation);
	}
}
