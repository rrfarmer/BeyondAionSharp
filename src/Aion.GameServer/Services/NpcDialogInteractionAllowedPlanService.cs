using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum NpcDialogInteractionAllowedStatus
{
	Allowed,
	RejectedBySummonOwner,
	RejectedByFortCapture,
	RejectedByMissingSkill,
	RejectedByMissingItem,
	RejectedByMissingReturnItem,
	RejectedByAbyssRank,
	RejectedByAbyssRanking,
	RejectedByTargetLegionDominion,
	RejectedByLegionDominionNpc,
	RejectedByLevel,
	RejectedByLevelLow,
	RejectedByLevelHigh,
	RejectedByUnhandledSubDialog,
}

public enum NpcDialogSummonOwnerType
{
	Private,
	Group,
	Legion,
	Alliance,
}

public sealed record NpcDialogInteractionAllowedInput(
	int PlayerObjectId,
	int NpcCreatorObjectId = 0,
	NpcDialogSummonOwnerType? SummonOwnerType = null,
	NpcSubDialogType? SubDialogType = null,
	int SubDialogValue = 0,
	bool PlayerGroupHasCreator = false,
	bool PlayerAllianceHasCreator = false,
	bool PlayerLegionHasCreator = false,
	bool PlayerHasLegion = false,
	bool FortZoneFound = true,
	bool FortressCapturedByPlayerLegion = false,
	bool PlayerHasSubDialogSkill = false,
	bool PlayerHasSubDialogItem = false,
	bool PlayerHasReturnItem = false,
	bool PlayerIsStaff = false,
	int PlayerAbyssRankId = 0,
	int PlayerAbyssRankingPosition = int.MaxValue,
	bool LegionDominionCalculationActive = false,
	int PlayerCurrentLegionDominion = 0,
	int PlayerOccupiedLegionDominion = 0,
	int PlayerLevel = 0);

public sealed record NpcDialogInteractionAllowedPlan(
	NpcDialogInteractionAllowedStatus Status,
	bool IsAllowed,
	string JavaSource,
	bool IsLive = false);

public static class NpcDialogInteractionAllowedPlanService
{
	private const int AbbeyReturnStoneItemId = 164000335;

	public static NpcDialogInteractionAllowedPlan CreatePlan(NpcDialogInteractionAllowedInput input)
	{
		// Java parity breadcrumb: services/DialogService.isInteractionAllowed,
		// isSummonOwner, and isSubDialogRestricted. This is a pure planner:
		// no live group/alliance/legion, zone, siege, inventory, skill, ranking,
		// logging, or packet side effects are executed.
		if (input.SummonOwnerType != null && !IsSummonOwner(input))
		{
			return Rejected(
				NpcDialogInteractionAllowedStatus.RejectedBySummonOwner,
				"DialogService.isInteractionAllowed -> npc.getSummonOwner != null && !isSummonOwner");
		}

		if (input.SubDialogType == null)
		{
			return Allowed("DialogService.isSubDialogRestricted -> talkInfo/subDialogType null");
		}

		return input.SubDialogType switch
		{
			NpcSubDialogType.FortCapture => CreateFortCapturePlan(input),
			NpcSubDialogType.SkillId => input.PlayerHasSubDialogSkill
				? Allowed("DialogService.isSubDialogRestricted SKILL_ID")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByMissingSkill, "player.getSkillList().getSkillEntry(subDialogValue) == null"),
			NpcSubDialogType.ItemId => input.PlayerHasSubDialogItem
				? Allowed("DialogService.isSubDialogRestricted ITEM_ID")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByMissingItem, "player.getInventory().getItemCountByItemId(subDialogValue) == 0"),
			NpcSubDialogType.Return => input.PlayerHasReturnItem
				? Allowed($"DialogService.isSubDialogRestricted RETURN item {AbbeyReturnStoneItemId}")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByMissingReturnItem, "player.getInventory().getItemCountByItemId(164000335) == 0"),
			NpcSubDialogType.AbyssRank => input.PlayerIsStaff || input.PlayerAbyssRankId >= input.SubDialogValue
				? Allowed("DialogService.isSubDialogRestricted ABYSSRANK")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByAbyssRank, "player.getAbyssRank().getRank().getId() < subDialogValue"),
			NpcSubDialogType.AbyssRanking => input.PlayerAbyssRankingPosition <= input.SubDialogValue
				? Allowed("DialogService.isSubDialogRestricted ABYSSRANKING")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByAbyssRanking, "AbyssRankingCache.getRankingListPosition(player) > subDialogValue"),
			NpcSubDialogType.TargetLegionDominion => CreateTargetLegionDominionPlan(input),
			NpcSubDialogType.LegionDominionNpc => CreateLegionDominionNpcPlan(input),
			NpcSubDialogType.Level => input.PlayerLevel == input.SubDialogValue
				? Allowed("DialogService.isSubDialogRestricted LEVEL")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByLevel, "player.getLevel() != subDialogValue"),
			NpcSubDialogType.LevelLow => input.PlayerLevel <= input.SubDialogValue
				? Allowed("DialogService.isSubDialogRestricted LEVEL_LOW")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByLevelLow, "player.getLevel() > subDialogValue"),
			NpcSubDialogType.LevelHigh => input.PlayerLevel >= input.SubDialogValue
				? Allowed("DialogService.isSubDialogRestricted LEVEL_HIGH")
				: Rejected(NpcDialogInteractionAllowedStatus.RejectedByLevelHigh, "player.getLevel() < subDialogValue"),
			_ => Rejected(
				NpcDialogInteractionAllowedStatus.RejectedByUnhandledSubDialog,
				"DialogService.isSubDialogRestricted default unhandled subdialog warning"),
		};
	}

	private static bool IsSummonOwner(NpcDialogInteractionAllowedInput input)
	{
		var playerIsCreator = input.PlayerObjectId == input.NpcCreatorObjectId;
		return input.SummonOwnerType switch
		{
			NpcDialogSummonOwnerType.Private => playerIsCreator,
			NpcDialogSummonOwnerType.Group => playerIsCreator || input.PlayerGroupHasCreator,
			NpcDialogSummonOwnerType.Alliance => playerIsCreator || input.PlayerAllianceHasCreator,
			NpcDialogSummonOwnerType.Legion => playerIsCreator || input.PlayerLegionHasCreator,
			_ => false,
		};
	}

	private static NpcDialogInteractionAllowedPlan CreateFortCapturePlan(NpcDialogInteractionAllowedInput input)
	{
		if (!input.PlayerHasLegion || !input.FortZoneFound || !input.FortressCapturedByPlayerLegion)
		{
			return Rejected(
				NpcDialogInteractionAllowedStatus.RejectedByFortCapture,
				"DialogService.isSubDialogRestricted FORT_CAPTURE");
		}

		return Allowed("DialogService.isSubDialogRestricted FORT_CAPTURE");
	}

	private static NpcDialogInteractionAllowedPlan CreateTargetLegionDominionPlan(NpcDialogInteractionAllowedInput input)
	{
		if (input.LegionDominionCalculationActive
			|| !input.PlayerHasLegion
			|| input.PlayerCurrentLegionDominion != input.SubDialogValue)
		{
			return Rejected(
				NpcDialogInteractionAllowedStatus.RejectedByTargetLegionDominion,
				"DialogService.isSubDialogRestricted TARGET_LEGION_DOMINION");
		}

		return Allowed("DialogService.isSubDialogRestricted TARGET_LEGION_DOMINION");
	}

	private static NpcDialogInteractionAllowedPlan CreateLegionDominionNpcPlan(NpcDialogInteractionAllowedInput input)
	{
		if (!input.PlayerHasLegion || input.PlayerOccupiedLegionDominion != input.SubDialogValue)
		{
			return Rejected(
				NpcDialogInteractionAllowedStatus.RejectedByLegionDominionNpc,
				"DialogService.isSubDialogRestricted LEGION_DOMINION_NPC");
		}

		return Allowed("DialogService.isSubDialogRestricted LEGION_DOMINION_NPC");
	}

	private static NpcDialogInteractionAllowedPlan Allowed(string javaSource)
	{
		return new NpcDialogInteractionAllowedPlan(
			NpcDialogInteractionAllowedStatus.Allowed,
			IsAllowed: true,
			javaSource,
			IsLive: false);
	}

	private static NpcDialogInteractionAllowedPlan Rejected(
		NpcDialogInteractionAllowedStatus status,
		string javaSource)
	{
		return new NpcDialogInteractionAllowedPlan(
			status,
			IsAllowed: false,
			javaSource,
			IsLive: false);
	}
}
