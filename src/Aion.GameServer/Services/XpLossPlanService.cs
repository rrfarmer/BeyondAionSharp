namespace Aion.GameServer.Services;

public sealed record XpLossPlan(
	int PlayerLevel,
	long ExpNeed,
	long ExpLoss,
	double Param,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class XpLossPlanService
{
	// Java parity: utils/stats/XPLossEnum entries.
	// (maxLevel, param) — find first entry where playerLevel <= maxLevel.
	private static readonly (int MaxLevel, double Param)[] Brackets =
	[
		(6,  1.0),
		(30, 1.0),
		(40, 0.35),
		(50, 0.25),
		(55, 0.25),
		(60, 0.25),
		(65, 0.25),
	];

	public static XpLossPlan CreatePlan(int playerLevel, long expNeed)
	{
		// Java parity: utils/stats/XPLossEnum.getExpLoss(int, long).
		if (playerLevel < 6)
		{
			return new XpLossPlan(playerLevel, expNeed, ExpLoss: 0, Param: 0,
				"XPLossEnum.getExpLoss -> level < 6 -> return 0");
		}

		foreach (var (maxLevel, param) in Brackets)
		{
			if (playerLevel <= maxLevel)
			{
				var loss = (long)Math.Round(expNeed / 100.0 * param);
				return new XpLossPlan(playerLevel, expNeed, loss, param,
					$"XPLossEnum.getExpLoss -> level {playerLevel} <= {maxLevel} -> round({expNeed}/100 * {param}) = {loss}");
			}
		}

		// No match (level > 65 in original Java) → return 0
		return new XpLossPlan(playerLevel, expNeed, ExpLoss: 0, Param: 0,
			$"XPLossEnum.getExpLoss -> level {playerLevel} > 65 -> return 0");
	}
}
