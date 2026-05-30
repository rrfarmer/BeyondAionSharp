namespace Aion.GameServer.Services;

public enum PlayerReviveStatsPlanStatus
{
	PlanCreated,
}

public sealed record PlayerReviveStatsPlan(
	PlayerReviveStatsPlanStatus Status,
	int HpPercent,
	int MpPercent,
	int RestoredHp,
	int RestoredMp,
	bool IsNoResurrectPenalty,
	bool ShouldClearDp,
	bool ShouldSetSoulSickness,
	int ResurrectionSkillId,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PlayerReviveStatsPlanService
{
	// Java parity: services/player/PlayerReviveService method constants.
	public const int DuelReviveHpPercent = 30;
	public const int DuelReviveMpPercent = 30;
	public const int SkillReviveHpPercent = 35;
	public const int SkillReviveMpPercent = 35;
	public const int NoResurrectPenaltyPercent = 100;
	public const int FallbackRebirthPercent = 5;

	public static PlayerReviveStatsPlan CreatePlan(
		int hpPercent,
		int mpPercent,
		int maxHp,
		int maxMp,
		int currentDp,
		bool isNoResurrectPenalty,
		bool setSoulSickness,
		int resurrectionSkillId = 0)
	{
		// Java parity: services/player/PlayerReviveService.revive(Player, int, int, boolean, int).
		// isNoResurrectPenalty overrides percent to 100.
		var effectiveHpPercent = isNoResurrectPenalty ? NoResurrectPenaltyPercent : hpPercent;
		var effectiveMpPercent = isNoResurrectPenalty ? NoResurrectPenaltyPercent : mpPercent;

		// Java: setCurrentHpPercent → maxHp * percent / 100
		var restoredHp = maxHp * effectiveHpPercent / 100;
		var restoredMp = maxMp * effectiveMpPercent / 100;

		// Java: if (player.getCommonData().getDp() > 0 && !isNoResurrectPenalty) setDp(0)
		var shouldClearDp = currentDp > 0 && !isNoResurrectPenalty;

		// Java: if (!isNoResurrectPenalty && setSoulSickness) updateSoulSickness(resurrectionSkill)
		var shouldSetSoulSickness = !isNoResurrectPenalty && setSoulSickness;

		return new PlayerReviveStatsPlan(
			PlayerReviveStatsPlanStatus.PlanCreated,
			effectiveHpPercent,
			effectiveMpPercent,
			restoredHp,
			restoredMp,
			isNoResurrectPenalty,
			shouldClearDp,
			shouldSetSoulSickness,
			resurrectionSkillId,
			isNoResurrectPenalty
				? $"PlayerReviveService.revive -> isNoResurrectPenalty=true -> HP=100% ({restoredHp}), MP=100% ({restoredMp}), no DP clear, no soul sickness"
				: $"PlayerReviveService.revive -> HP={effectiveHpPercent}% ({restoredHp}), MP={effectiveMpPercent}% ({restoredMp}), clearDp={shouldClearDp}, soulSickness={shouldSetSoulSickness}"
		);
	}
}
