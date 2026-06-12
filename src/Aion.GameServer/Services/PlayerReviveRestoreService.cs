using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerReviveRestoreService
{
	public const int KiskReviveHpPercent = 30;
	public const int KiskReviveMpPercent = 30;
	public const int BindReviveHpPercent = 25;
	public const int BindReviveMpPercent = 25;
	public const int InstanceReviveHpPercent = 25;
	public const int InstanceReviveMpPercent = 25;

	public static PlayerReviveRestoreResult ApplyKiskReviveRestore(
		Player player,
		int maxHp,
		int maxMp,
		bool hasNoResurrectPenalty = false)
	{
		// Java parity: services/player/PlayerReviveService.kiskRevive -> revive(player, 30, 30, false, skillId).
		return ApplyReviveRestore(player, maxHp, maxMp, KiskReviveHpPercent, KiskReviveMpPercent, hasNoResurrectPenalty);
	}

	public static PlayerReviveRestoreResult ApplyBindReviveRestore(
		Player player,
		int maxHp,
		int maxMp,
		bool hasNoResurrectPenalty = false)
	{
		// Java parity: services/player/PlayerReviveService.bindRevive -> revive(player, 25, 25, true, skillId)
		// outside EVENT_MODE. Soul-sickness side effects remain outside this resource/state restore slice.
		return ApplyReviveRestore(player, maxHp, maxMp, BindReviveHpPercent, BindReviveMpPercent, hasNoResurrectPenalty);
	}

	public static PlayerReviveRestoreResult ApplyInstanceReviveRestore(
		Player player,
		int maxHp,
		int maxMp,
		bool hasNoResurrectPenalty = false)
	{
		// Java parity: services/player/PlayerReviveService.instanceRevive -> revive(player, 25, 25, true, skillId)
		// when the instance handler does not consume the revive event.
		return ApplyReviveRestore(player, maxHp, maxMp, InstanceReviveHpPercent, InstanceReviveMpPercent, hasNoResurrectPenalty);
	}

	public static PlayerReviveRestoreResult ApplyReviveRestore(
		Player player,
		int maxHp,
		int maxMp,
		int hpPercent,
		int mpPercent,
		bool hasNoResurrectPenalty = false)
	{
		// Java parity: PlayerReviveService.revive handles no-resurrect-penalty, clears player-res state/skill,
		// then PlayerController.onBeforeSpawn clears FLOATING_CORPSE for flying deaths or DEAD otherwise.
		var previousLifeStats = player.LifeStats ?? new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0);
		var previousState = player.CreatureState;
		var previousDp = player.Dp;
		var previousPlayerResurrectionActive = player.IsPlayerResurrectionActive;
		var previousResurrectionSkillId = (player.GetResurrectionSkill());
		var normalizedMaxHp = Math.Max(0, maxHp);
		var normalizedMaxMp = Math.Max(0, maxMp);
		var effectiveHpPercent = hasNoResurrectPenalty ? 100 : hpPercent;
		var effectiveMpPercent = hasNoResurrectPenalty ? 100 : mpPercent;
		var nextLifeStats = previousLifeStats with
		{
			CurrentHp = CalculatePercentValue(normalizedMaxHp, effectiveHpPercent),
			CurrentMp = CalculatePercentValue(normalizedMaxMp, effectiveMpPercent),
		};

		player.IsPlayerResurrectionActive = false;
		if (!hasNoResurrectPenalty && player.Dp > 0)
			player.Dp = 0;
		player.SetResurrectionSkill(0);
		player.LifeStats = nextLifeStats;
		if (player.IsFlyingBeforeDeath)
			player.SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: false);
		else if (player.IsInState(PlayerCreatureState.Dead))
			player.SetCreatureState(PlayerCreatureState.Dead, enabled: false);
		player.SetCreatureState(PlayerCreatureState.Active, enabled: true);

		return new PlayerReviveRestoreResult(
			previousLifeStats,
			nextLifeStats,
			previousState,
			player.CreatureState,
			normalizedMaxHp,
			normalizedMaxMp,
			effectiveHpPercent,
			effectiveMpPercent,
			hasNoResurrectPenalty,
			previousDp,
			player.Dp,
			previousPlayerResurrectionActive,
			player.IsPlayerResurrectionActive,
			previousResurrectionSkillId,
			(player.GetResurrectionSkill()));
	}

	private static int CalculatePercentValue(int maxValue, int percent)
	{
		// Java parity: CreatureLifeStats.setCurrentHpPercent uses integer truncation after (long) max * percent / 100.
		return Math.Clamp((int)((long)maxValue * Math.Max(0, percent) / 100), 0, maxValue);
	}
}

public sealed record PlayerReviveRestoreResult(
	PlayerLifeStats PreviousLifeStats,
	PlayerLifeStats CurrentLifeStats,
	PlayerCreatureState PreviousState,
	PlayerCreatureState CurrentState,
	int MaxHp,
	int MaxMp,
	int HpPercent,
	int MpPercent,
	bool HasNoResurrectPenalty,
	int PreviousDp,
	int CurrentDp,
	bool PreviousPlayerResurrectionActive,
	bool CurrentPlayerResurrectionActive,
	int PreviousResurrectionSkillId,
	int CurrentResurrectionSkillId);
