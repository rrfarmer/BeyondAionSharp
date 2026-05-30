namespace Aion.GameServer.Services;

public enum LootDistributionType
{
	None = 0,
	Roll = 2,
	Bid = 3,
}

public sealed record LootGroupDistributionPlan(
	LootDistributionType DistributionType,
	int MythicItemAboveValue,
	bool IsBid,
	bool IsRoll,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record LootQualityRulePlan(
	string ItemQuality,
	int QualityThreshold,
	bool IsDistributionActive,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class LootGroupDistributionPlanService
{
	// Java parity: model/team/common/legacy/LootGroupRules.getAutodistributionId.
	// mythicItemAbove == 3 → bid; mythicItemAbove == 2 → roll; else → none.
	public static LootGroupDistributionPlan CreateDistributionPlan(int mythicItemAbove)
	{
		var isBid = mythicItemAbove == 3;
		var isRoll = mythicItemAbove == 2;
		var type = isBid ? LootDistributionType.Bid : isRoll ? LootDistributionType.Roll : LootDistributionType.None;

		return new LootGroupDistributionPlan(
			type, mythicItemAbove, isBid, isRoll,
			$"LootGroupRules.getAutodistributionId -> mythicItemAbove={mythicItemAbove} -> {type} (id={(int)type})"
		);
	}

	// Java parity: model/team/common/legacy/LootGroupRules.getQualityRule(ItemQuality).
	// Returns true if the quality's threshold is not 0.
	public static LootQualityRulePlan CreateQualityRulePlan(string itemQuality, int qualityThreshold)
	{
		var isActive = qualityThreshold != 0;
		return new LootQualityRulePlan(
			itemQuality, qualityThreshold, isActive,
			isActive
				? $"LootGroupRules.getQualityRule({itemQuality}) -> threshold={qualityThreshold} != 0 -> true (distribute)"
				: $"LootGroupRules.getQualityRule({itemQuality}) -> threshold={qualityThreshold} == 0 -> false (no distribution)"
		);
	}
}
