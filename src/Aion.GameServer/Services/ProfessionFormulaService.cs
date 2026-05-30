using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed record ProfessionUpgradeCostPlan(
	string ProfessionName,
	bool IsCrafting,
	int SkillLevel,
	int? UpgradeCost,
	bool CanUpgrade,
	string BlockReason,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ProfessionFormulaService
{
	// Java parity: model/craft/Profession.getUpgradeCost.
	public static int? GetUpgradeCost(int skillLevel, bool isCrafting)
	{
		return skillLevel switch
		{
			0 => 3500,
			99 => 17_000,
			199 => 115_000,
			299 => 460_000,
			449 => isCrafting ? 6_004_900 : null,
			_ => null,
		};
	}

	// Java parity: model/craft/Profession.isCrafting.
	public static bool IsCrafting(int skillId)
	{
		return skillId >= 40001 && skillId <= 40010;
	}

	// Java parity: model/craft/Profession.getMaxUpgradableLevel.
	public static int GetMaxUpgradableLevel(bool isCrafting)
	{
		return isCrafting ? 499 : 399;
	}

	// Java parity: model/craft/Profession.getSkillGrade(int skillLevel).
	// Returns ChatUtil.l10n encoded grade string. L10n IDs from Java source.
	public static string? GetSkillGradeL10n(int skillLevel, bool isCrafting)
	{
		if (skillLevel <= 99) return ChatUtil.L10n(900797);   // Amateur
		if (skillLevel <= 199) return ChatUtil.L10n(900798);  // Novice
		if (skillLevel <= 299) return ChatUtil.L10n(900799);  // Apprentice
		if (skillLevel <= 399) return ChatUtil.L10n(900800);  // Journeyman
		if (skillLevel <= 449) return ChatUtil.L10n(900801);  // Expert
		if (isCrafting && skillLevel <= 499) return ChatUtil.L10n(902027); // Artisan
		return ChatUtil.L10n(902028);                          // Master
	}

	public static ProfessionUpgradeCostPlan CreatePlan(
		string professionName,
		int skillId,
		int skillLevel)
	{
		// Java parity: services/craft/CraftSkillUpdateService.learnSkill guard chain.
		var isCrafting = IsCrafting(skillId);
		var cost = GetUpgradeCost(skillLevel, isCrafting);
		var maxLevel = GetMaxUpgradableLevel(isCrafting);

		string blockReason = string.Empty;
		if (cost == null)
		{
			if (skillLevel > maxLevel)
				blockReason = "STR_MSG_DONT_RANK_UP_GATHERING";
			else if (skillLevel == 399)
				blockReason = "STR_CRAFT_CANT_EXTEND_MONEY"; // expert by quest
			else if (skillLevel == 499)
				blockReason = "STR_CRAFT_CANT_EXTEND_GRAND_MASTER"; // master by quest
			else
				blockReason = "STR_MSG_DONT_RANK_UP";
		}

		return new ProfessionUpgradeCostPlan(
			professionName, isCrafting, skillLevel,
			cost, cost.HasValue, blockReason,
			cost.HasValue
				? $"Profession.getUpgradeCost({skillLevel}) -> {cost}"
				: $"Profession.getUpgradeCost({skillLevel}) -> null ({blockReason})"
		);
	}
}
