namespace Aion.GameServer.Services;

public sealed record NpcLevelDiffModPlan(
	int TargetLevel,
	int AttackerLevel,
	int LevelDifference,
	float Modifier,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class NpcLevelDiffModPlanService
{
	// Java parity: utils/stats/StatFunctions.getNpcLevelDiffMod.
	// Returns a multiplier used to adjust NPC damage and dodge rates based on level difference.
	public const int MinLevelDiffForModifier = 2;
	public const int MaxLevelDiffCap = 12;

	public static float GetNpcLevelDiffMod(int levelDiff)
	{
		if (levelDiff <= MinLevelDiffForModifier)
			return 0f;
		if (levelDiff >= MaxLevelDiffCap)
			return 1f;
		return (levelDiff - MinLevelDiffForModifier) * 0.1f;
	}

	public static NpcLevelDiffModPlan CreatePlan(int targetLevel, int attackerLevel)
	{
		// Java parity: StatFunctions.getNpcLevelDiffMod(target, attacker).
		// Used in: damage *= 1 - mod (incoming damage reduction for player attacking high-level NPC)
		// and: dodgeRate *= 1 + mod (NPC dodge boost against lower-level attacker)
		var levelDiff = targetLevel - attackerLevel;
		var mod = GetNpcLevelDiffMod(levelDiff);

		return new NpcLevelDiffModPlan(
			targetLevel, attackerLevel, levelDiff, mod,
			mod == 0f
				? $"StatFunctions.getNpcLevelDiffMod -> levelDiff({levelDiff}) <= 2 -> 0f"
				: mod == 1f
					? $"StatFunctions.getNpcLevelDiffMod -> levelDiff({levelDiff}) >= 12 -> 1f"
					: $"StatFunctions.getNpcLevelDiffMod -> ({levelDiff}-2)*0.1 = {mod:F1}f"
		);
	}
}
