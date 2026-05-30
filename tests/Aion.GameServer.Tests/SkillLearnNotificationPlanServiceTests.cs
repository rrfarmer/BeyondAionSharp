using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillLearnNotificationPlanServiceTests
{
	// --- Normal skill message IDs ---

	[Fact]
	public void GetSkillLearnMessageId_NewNormalSkillUses1300050()
	{
		var skill = CreateNormalSkill(skillId: 100, skillLevel: 1);
		Assert.Equal(1300050, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: true));
	}

	[Fact]
	public void GetSkillLearnMessageId_UpgradeNormalSkillUsesZero()
	{
		var skill = CreateNormalSkill(skillId: 100, skillLevel: 2);
		Assert.Equal(0, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: false));
	}

	[Fact]
	public void GetSkillLearnMessageId_NewNormalStigmaSkillUses1300401()
	{
		var skill = CreateStigmaSkill(skillType: 1);
		Assert.Equal(1300401, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: true));
	}

	[Fact]
	public void GetSkillLearnMessageId_NewLinkedStigmaSkillUses1402891()
	{
		var skill = CreateStigmaSkill(skillType: 3);
		Assert.Equal(1402891, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: true));
	}

	[Fact]
	public void GetSkillLearnMessageId_UpgradeStigmaSkillUsesZero()
	{
		var skill = CreateStigmaSkill(skillType: 1);
		Assert.Equal(0, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: false));
	}

	// --- Tapping skill message IDs (profession, IsTappingSkill = skillId 30001-30003) ---

	[Fact]
	public void GetSkillLearnMessageId_NewTappingSkillUses1330004()
	{
		var skill = CreateProfessionSkill(skillId: 30001, skillLevel: 1);
		Assert.Equal(1330004, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: true));
	}

	[Fact]
	public void GetSkillLearnMessageId_UpgradeTappingSkillUses1330005()
	{
		var skill = CreateProfessionSkill(skillId: 30001, skillLevel: 2);
		Assert.Equal(1330005, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: false));
	}

	// --- Non-tapping profession skill message IDs (crafting, morphing) ---

	[Fact]
	public void GetSkillLearnMessageId_NewCraftingSkillUses1330061()
	{
		var skill = CreateProfessionSkill(skillId: 40001, skillLevel: 1);
		Assert.Equal(1330061, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: true));
	}

	[Fact]
	public void GetSkillLearnMessageId_UpgradeCraftingSkillUses1330064()
	{
		var skill = CreateProfessionSkill(skillId: 40001, skillLevel: 100);
		Assert.Equal(1330064, SkillLearnNotificationPlanService.GetSkillLearnMessageId(skill, isNew: false));
	}

	// --- CRAFT_LEVEL_UP broadcast thresholds ---

	[Theory]
	[InlineData(100)]
	[InlineData(200)]
	[InlineData(300)]
	[InlineData(400)]
	[InlineData(450)]
	[InlineData(500)]
	public void CreatePlan_BroadcastsCraftLevelUpAtProfessionLevelThresholds(int skillLevel)
	{
		// Java: onLearnSkill broadcasts CRAFT_LEVEL_UP at 100,200,300,400,450,500 for all profession skills
		var skill = CreateProfessionSkill(skillId: 40001, skillLevel: skillLevel);
		var plan = SkillLearnNotificationPlanService.CreatePlan(skill, isNew: false, playerObjectId: 9001);

		Assert.Equal(SkillLearnNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.True(plan.ShouldBroadcastCraftLevelUp);
		Assert.NotNull(plan.CraftLevelUpAnimationPacket);
		Assert.Contains("CRAFT_LEVEL_UP", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_BroadcastsCraftLevelUpAtLevel1OnlyCraftingSkills()
	{
		// Java: level 1 is excluded except for crafting skills (isCraftingSkill check)
		var craftingSkill = CreateProfessionSkill(skillId: 40001, skillLevel: 1);
		var tappingSkill = CreateProfessionSkill(skillId: 30001, skillLevel: 1);

		var craftPlan = SkillLearnNotificationPlanService.CreatePlan(craftingSkill, isNew: true, playerObjectId: 9001);
		var tappingPlan = SkillLearnNotificationPlanService.CreatePlan(tappingSkill, isNew: true, playerObjectId: 9001);

		Assert.True(craftPlan.ShouldBroadcastCraftLevelUp);
		Assert.False(tappingPlan.ShouldBroadcastCraftLevelUp);
	}

	[Fact]
	public void CreatePlan_DoesNotBroadcastCraftLevelUpForNormalSkills()
	{
		var skill = CreateNormalSkill(skillId: 100, skillLevel: 100);
		var plan = SkillLearnNotificationPlanService.CreatePlan(skill, isNew: false, playerObjectId: 9001);

		Assert.False(plan.ShouldBroadcastCraftLevelUp);
		Assert.Null(plan.CraftLevelUpAnimationPacket);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(50)]
	[InlineData(99)]
	[InlineData(150)]
	public void CreatePlan_DoesNotBroadcastCraftLevelUpAtNonThresholdLevels(int skillLevel)
	{
		var skill = CreateProfessionSkill(skillId: 40001, skillLevel: skillLevel);
		var plan = SkillLearnNotificationPlanService.CreatePlan(skill, isNew: false, playerObjectId: 9001);

		Assert.False(plan.ShouldBroadcastCraftLevelUp);
	}

	// --- Plan structure ---

	[Fact]
	public void CreatePlan_AlwaysSendsSkillListToPlayer()
	{
		var skill = CreateNormalSkill(skillId: 100, skillLevel: 1);
		var plan = SkillLearnNotificationPlanService.CreatePlan(skill, isNew: true, playerObjectId: 9001);

		Assert.Equal(SkillLearnNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToPlayer);
		Assert.NotNull(plan.SkillListPacket);
	}

	private static PlayerSkill CreateNormalSkill(int skillId, int skillLevel)
	{
		return new PlayerSkill { SkillId = skillId, SkillLevel = skillLevel, SkillType = 0 };
	}

	private static PlayerSkill CreateStigmaSkill(int skillType)
	{
		return new PlayerSkill { SkillId = 200, SkillLevel = 1, SkillType = skillType };
	}

	private static PlayerSkill CreateProfessionSkill(int skillId, int skillLevel)
	{
		return new PlayerSkill { SkillId = skillId, SkillLevel = skillLevel, SkillType = 0 };
	}
}
