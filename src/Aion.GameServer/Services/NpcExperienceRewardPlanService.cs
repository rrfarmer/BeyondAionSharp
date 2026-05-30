namespace Aion.GameServer.Services;

public sealed record NpcExperienceRewardPlan(
	int BaseXp,
	float MapMultiplier,
	int MaxPlayers,
	bool IsGroupInstance,
	float XpSoloRate,
	int XpPercentage,
	float EffectiveMapMultiplier,
	long RewardXp,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class NpcExperienceRewardPlanService
{
	public static NpcExperienceRewardPlan CreatePlan(
		int baseXp,
		float baseMapMultiplier,
		bool isGroupInstance,
		int maxPlayers,
		float xpSoloRate,
		int xpPercentage)
	{
		// Java parity: utils/stats/StatFunctions.calculateExperienceReward.
		// For group instances (2-6 players): mapMulti *= maxPlayers / XP_SOLO_RATES[0]
		float mapMulti = baseMapMultiplier;
		if (isGroupInstance && maxPlayers >= 2 && maxPlayers <= 6)
		{
			mapMulti *= maxPlayers;
			mapMulti /= xpSoloRate;
		}

		var rewardXp = (long)Math.Round(baseXp * mapMulti * (xpPercentage / 100f));

		return new NpcExperienceRewardPlan(
			baseXp, baseMapMultiplier, maxPlayers, isGroupInstance, xpSoloRate, xpPercentage,
			mapMulti, rewardXp,
			$"StatFunctions.calculateExperienceReward -> baseXP={baseXp} * mapMulti={mapMulti:F3} * xp%({xpPercentage}/100) = {rewardXp}"
		);
	}
}
