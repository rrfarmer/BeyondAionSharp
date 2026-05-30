using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SkillLearnNotificationPlanStatus
{
	PlanCreated,
}

public sealed record SkillLearnNotificationPlan(
	SkillLearnNotificationPlanStatus Status,
	int MessageId,
	bool IsNew,
	SmSkillList SkillListPacket,
	SmActionAnimation? CraftLevelUpAnimationPacket,
	bool ShouldSendToPlayer,
	bool ShouldBroadcastCraftLevelUp,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SkillLearnNotificationPlanService
{
	// Java parity: services/SkillLearnService.sendPacket message id thresholds for craft level announcements.
	private static readonly IReadOnlySet<int> CraftLevelUpBroadcastLevels =
		new HashSet<int> { 100, 200, 300, 400, 450, 500 };

	public static SkillLearnNotificationPlan CreatePlan(
		PlayerSkill skill,
		bool isNew,
		int playerObjectId,
		DateTimeOffset? clockOverride = null)
	{
		// Java parity: services/SkillLearnService.sendPacket and onLearnSkill broadcast logic.
		var messageId = GetSkillLearnMessageId(skill, isNew);
		var clock = clockOverride.HasValue ? () => clockOverride.Value : (Func<DateTimeOffset>?)null;
		var skillListPacket = new SmSkillList([skill], messageId, clock);

		var shouldBroadcastCraftLevelUp = ShouldBroadcastCraftLevelUp(skill, isNew);
		SmActionAnimation? craftLevelUpPacket = null;
		if (shouldBroadcastCraftLevelUp)
		{
			craftLevelUpPacket = new SmActionAnimation(
				playerObjectId,
				SmActionAnimation.CraftLevelUp);
		}

		return new SkillLearnNotificationPlan(
			SkillLearnNotificationPlanStatus.PlanCreated,
			messageId,
			isNew,
			skillListPacket,
			craftLevelUpPacket,
			ShouldSendToPlayer: true,
			shouldBroadcastCraftLevelUp,
			BuildJavaSource(skill, isNew, shouldBroadcastCraftLevelUp));
	}

	public static int GetSkillLearnMessageId(PlayerSkill skill, bool isNew)
	{
		// Java parity: services/SkillLearnService.sendPacket message id branches.
		if (skill.IsProfessionSkill)
		{
			if (skill.IsTappingSkill)
				return isNew ? 1330004 : 1330005;
			return isNew ? 1330061 : 1330064;
		}

		if (!isNew)
			return 0;

		if (!skill.IsStigmaSkill)
			return 1300050;

		return IsLinkedStigmaSkill(skill) ? 1402891 : 1300401;
	}

	private static bool ShouldBroadcastCraftLevelUp(PlayerSkill skill, bool isNew)
	{
		// Java parity: services/SkillLearnService.onLearnSkill broadcasts CRAFT_LEVEL_UP only on specific
		// profession skill level thresholds; level 1 is excluded except for crafting skills.
		// isNew does not affect the craft level broadcast (Java always checks isProfessionSkill + skillLevel).
		if (!skill.IsProfessionSkill)
			return false;

		if (skill.SkillLevel == 1)
			return skill.IsCraftingSkill;

		return CraftLevelUpBroadcastLevels.Contains(skill.SkillLevel);
	}

	private static bool IsLinkedStigmaSkill(PlayerSkill skill)
	{
		// Java parity: model/skill/PlayerSkillEntry.isLinkedStigmaSkill -> skillType >= 3.
		return skill.SkillType >= 3;
	}

	private static string BuildJavaSource(PlayerSkill skill, bool isNew, bool broadcastsCraftLevelUp)
	{
		var baseSource = $"SkillLearnService.sendPacket -> SM_SKILL_LIST(skill, {GetSkillLearnMessageId(skill, isNew)}) isNew={isNew}";
		if (broadcastsCraftLevelUp)
			return baseSource + " + onLearnSkill -> broadcastPacket(SM_ACTION_ANIMATION CRAFT_LEVEL_UP)";
		return baseSource;
	}
}
