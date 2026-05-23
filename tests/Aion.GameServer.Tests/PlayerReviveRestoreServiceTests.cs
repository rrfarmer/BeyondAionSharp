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
	public void CalculateCurrentResourceMaxStatsExposesSmStatsInfoMaxResourceSource()
	{
		var player = new Player { PlayerClass = string.Empty };

		var stats = SmStatsInfo.CalculateCurrentResourceMaxStats(player);

		Assert.Equal(244, stats.MaxHp);
		Assert.Equal(210, stats.MaxMp);
		Assert.Equal(60, stats.MaxFp);
	}
}
