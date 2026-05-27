using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerReviveRestoreServiceTests
{
	[Fact]
	public void ApplyKiskReviveRestoreMatchesJavaPercentRestoreAndDeadStateClear()
	{
		var player = new Player
		{
			CreatureState = PlayerCreatureState.Dead | PlayerCreatureState.WalkMode | PlayerCreatureState.Powershard,
			Dp = 900,
			IsPlayerResurrectionActive = true,
			ResurrectionSkillId = 4456,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 7, CurrentFp: 42),
		};

		var result = PlayerReviveRestoreService.ApplyKiskReviveRestore(player, maxHp: 333, maxMp: 222);

		Assert.Equal(new PlayerLifeStats(0, 7, 42), result.PreviousLifeStats);
		Assert.Equal(new PlayerLifeStats(99, 66, 42), result.CurrentLifeStats);
		Assert.Equal(new PlayerLifeStats(99, 66, 42), player.LifeStats);
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsInState(PlayerCreatureState.WalkMode));
		Assert.True(player.IsInState(PlayerCreatureState.Powershard));
		Assert.Equal(PlayerReviveRestoreService.KiskReviveHpPercent, result.HpPercent);
		Assert.Equal(PlayerReviveRestoreService.KiskReviveMpPercent, result.MpPercent);
		Assert.False(result.HasNoResurrectPenalty);
		Assert.Equal(900, result.PreviousDp);
		Assert.Equal(0, result.CurrentDp);
		Assert.Equal(0, player.Dp);
		Assert.True(result.PreviousPlayerResurrectionActive);
		Assert.False(result.CurrentPlayerResurrectionActive);
		Assert.False(player.IsPlayerResurrectionActive);
		Assert.Equal(4456, result.PreviousResurrectionSkillId);
		Assert.Equal(0, result.CurrentResurrectionSkillId);
		Assert.Equal(0, player.ResurrectionSkillId);
	}

	[Fact]
	public void ApplyKiskReviveRestoreHonorsNoResurrectPenaltyLikeJavaRevive()
	{
		var player = new Player
		{
			CreatureState = PlayerCreatureState.Dead,
			Dp = 1200,
			IsPlayerResurrectionActive = true,
			ResurrectionSkillId = 4456,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 42),
		};

		var result = PlayerReviveRestoreService.ApplyKiskReviveRestore(
			player,
			maxHp: 333,
			maxMp: 222,
			hasNoResurrectPenalty: true);

		Assert.True(result.HasNoResurrectPenalty);
		Assert.Equal(100, result.HpPercent);
		Assert.Equal(100, result.MpPercent);
		Assert.Equal(new PlayerLifeStats(333, 222, 42), result.CurrentLifeStats);
		Assert.Equal(1200, result.PreviousDp);
		Assert.Equal(1200, result.CurrentDp);
		Assert.Equal(1200, player.Dp);
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.True(result.PreviousPlayerResurrectionActive);
		Assert.False(result.CurrentPlayerResurrectionActive);
		Assert.Equal(4456, result.PreviousResurrectionSkillId);
		Assert.Equal(0, result.CurrentResurrectionSkillId);
		Assert.Equal(0, player.ResurrectionSkillId);
	}

	[Fact]
	public void ApplyKiskReviveRestoreClearsFloatingCorpseForFlyingBeforeDeathLikeOnBeforeSpawn()
	{
		var player = new Player
		{
			CreatureState = PlayerCreatureState.FloatingCorpse | PlayerCreatureState.WalkMode,
			IsFlyingBeforeDeath = true,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 42),
		};

		var result = PlayerReviveRestoreService.ApplyKiskReviveRestore(player, maxHp: 333, maxMp: 222);

		Assert.True(result.PreviousState.HasFlag(PlayerCreatureState.FloatingCorpse));
		Assert.False(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsInState(PlayerCreatureState.WalkMode));
		Assert.True(player.IsFlyingBeforeDeath);
		Assert.Equal(new PlayerLifeStats(99, 66, 42), player.LifeStats);
	}

	[Fact]
	public void ApplyReviveRestoreClampsInvalidMaxAndPercentValuesLikeCreatureLifeStats()
	{
		var player = new Player
		{
			CreatureState = PlayerCreatureState.Dead,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 10),
		};

		var result = PlayerReviveRestoreService.ApplyReviveRestore(
			player,
			maxHp: -1,
			maxMp: 100,
			hpPercent: 200,
			mpPercent: -50);

		Assert.Equal(0, result.CurrentLifeStats.CurrentHp);
		Assert.Equal(0, result.CurrentLifeStats.CurrentMp);
		Assert.Equal(10, result.CurrentLifeStats.CurrentFp);
		Assert.Equal(0, result.MaxHp);
		Assert.Equal(100, result.MaxMp);
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
	}

	[Fact]
	public void ApplyReviveRestoreHonorsNoResurrectPenaltyForResourcesAndDp()
	{
		var player = new Player
		{
			CreatureState = PlayerCreatureState.Dead,
			Dp = 1200,
			IsPlayerResurrectionActive = true,
			ResurrectionSkillId = 9872,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 10),
		};

		var result = PlayerReviveRestoreService.ApplyReviveRestore(
			player,
			maxHp: 333,
			maxMp: 222,
			hpPercent: 30,
			mpPercent: 30,
			hasNoResurrectPenalty: true);

		Assert.True(result.HasNoResurrectPenalty);
		Assert.Equal(100, result.HpPercent);
		Assert.Equal(100, result.MpPercent);
		Assert.Equal(333, result.CurrentLifeStats.CurrentHp);
		Assert.Equal(222, result.CurrentLifeStats.CurrentMp);
		Assert.Equal(1200, result.PreviousDp);
		Assert.Equal(1200, result.CurrentDp);
		Assert.Equal(1200, player.Dp);
		Assert.True(result.PreviousPlayerResurrectionActive);
		Assert.False(result.CurrentPlayerResurrectionActive);
		Assert.Equal(9872, result.PreviousResurrectionSkillId);
		Assert.Equal(0, result.CurrentResurrectionSkillId);
	}

	[Fact]
	public void CalculateCurrentResourceMaxStatsExposesSmStatsInfoMaxResourceSource()
	{
		var player = new Player { PlayerClass = string.Empty };

		var stats = SmStatsInfo.CalculateCurrentResourceMaxStats(player);

		Assert.Equal(244, stats.MaxHp);
		Assert.Equal(210, stats.MaxMp);
		Assert.Equal(60, stats.MaxFp);
	}
}
