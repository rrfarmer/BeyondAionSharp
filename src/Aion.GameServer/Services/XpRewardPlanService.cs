namespace Aion.GameServer.Services;

public sealed record XpRewardPlan(
	int TargetLevel,
	int PlayerLevel,
	int LevelDifference,
	int XpRewardPercent,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class XpRewardPlanService
{
	public static int XpRewardFrom(int levelDifference)
	{
		if (levelDifference < -11) return 0;
		if (levelDifference > 4) return 120;
		return levelDifference switch
		{
			-11 => 0,
			-10 => 1,
			-9 => 10,
			-8 => 20,
			-7 => 30,
			-6 => 40,
			-5 => 50,
			-4 => 60,
			-3 => 90,
			-2 => 100,
			-1 => 100,
			0 => 100,
			1 => 105,
			2 => 110,
			3 => 115,
			4 => 120,
			_ => 100,
		};
	}

	public static XpRewardPlan CreatePlan(int targetLevel, int playerLevel)
	{
		// Java parity: utils/stats/XPRewardEnum.xpRewardFrom used in StatFunctions.calculateExperienceReward.
		var diff = targetLevel - playerLevel;
		var percent = XpRewardFrom(diff);
		return new XpRewardPlan(
			targetLevel, playerLevel, diff, percent,
			$"XPRewardEnum.xpRewardFrom(target({targetLevel})-player({playerLevel})={diff}) -> {percent}%"
		);
	}
}
