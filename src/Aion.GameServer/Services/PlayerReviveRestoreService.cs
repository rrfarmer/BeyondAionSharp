using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerReviveRestoreService
{
	public const int KiskReviveHpPercent = 30;
	public const int KiskReviveMpPercent = 30;

	public static PlayerReviveRestoreResult ApplyKiskReviveRestore(Player player, int maxHp, int maxMp)
	{
		// Java parity: services/player/PlayerReviveService.kiskRevive -> revive(player, 30, 30, false, skillId).
		return ApplyReviveRestore(player, maxHp, maxMp, KiskReviveHpPercent, KiskReviveMpPercent);
	}

	public static PlayerReviveRestoreResult ApplyReviveRestore(
		Player player,
		int maxHp,
		int maxMp,
		int hpPercent,
		int mpPercent)
	{
		// Java parity: PlayerReviveService.revive sets HP percent, then MP percent, then PlayerController.onBeforeSpawn clears DEAD state.
		var previousLifeStats = player.LifeStats ?? new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0);
		var previousState = player.CreatureState;
		var normalizedMaxHp = Math.Max(0, maxHp);
		var normalizedMaxMp = Math.Max(0, maxMp);
		var nextLifeStats = previousLifeStats with
		{
			CurrentHp = CalculatePercentValue(normalizedMaxHp, hpPercent),
			CurrentMp = CalculatePercentValue(normalizedMaxMp, mpPercent),
		};

		player.LifeStats = nextLifeStats;
		if (player.IsInState(PlayerCreatureState.Dead))
			player.SetCreatureState(PlayerCreatureState.Dead, enabled: false);
		player.SetCreatureState(PlayerCreatureState.Active, enabled: true);

		return new PlayerReviveRestoreResult(
			previousLifeStats,
			nextLifeStats,
			previousState,
			player.CreatureState,
			normalizedMaxHp,
			normalizedMaxMp,
			hpPercent,
			mpPercent);
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
	int MpPercent);
