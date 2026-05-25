using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class StonespearReachApRewardServiceTests
{
	[Theory]
	[InlineData(67_001, true, 1, 6_700)]
	[InlineData(67_001, false, 1, 0)]
	[InlineData(67_000, true, 2, 0)]
	[InlineData(41_001, false, 2, 0)]
	[InlineData(25_001, false, 3, 0)]
	[InlineData(8_801, false, 5, 0)]
	[InlineData(1_001, false, 6, 0)]
	[InlineData(1, false, 6, 0)]
	[InlineData(0, false, 8, 0)]
	public void CalculateFinalRankAndAp_MatchesJavaStrictThresholds(
		int points,
		bool bossKilled,
		int expectedRank,
		int expectedAp)
	{
		var rank = StonespearReachApRewardService.CalculateFinalRank(points);
		var finalAp = StonespearReachApRewardService.CalculateFinalAp(points, bossKilled);

		Assert.Equal(expectedRank, rank);
		Assert.Equal(expectedAp, finalAp);
	}

	[Fact]
	public void ApplyFinalApReward_AddsUnscaledBossKilledSRankApThroughPlanner()
	{
		var service = new StonespearReachApRewardService();
		var player = CreatePlayer(objectId: 1900, currentAp: 4_000);

		var result = service.ApplyFinalApReward(player, points: 67_001, bossKilled: true);

		Assert.Equal(StonespearReachApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(67_001, result.Points);
		Assert.True(result.BossKilled);
		Assert.Equal(1, result.Rank);
		Assert.Equal(6_700, result.FinalAp);
		Assert.Equal(4_000, result.PreviousAp);
		Assert.Equal(10_700, result.CurrentAp);
		Assert.Equal(10_700, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(6_700, result.AbyssPointsPlan.Added);
		Assert.Collection(
			result.AbyssPointsPlan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1320000, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public void ApplyFinalApReward_SkipsSRankWhenBossWasNotKilled()
	{
		var service = new StonespearReachApRewardService();
		var player = CreatePlayer(objectId: 1901, currentAp: 5_000);

		var result = service.ApplyFinalApReward(player, points: 67_001, bossKilled: false);

		Assert.Equal(StonespearReachApRewardStatus.NoApReward, result.Status);
		Assert.Equal(1, result.Rank);
		Assert.Equal(0, result.FinalAp);
		Assert.Equal(5_000, result.CurrentAp);
		Assert.Null(result.AbyssPointsPlan);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyFinalApReward_SkipsMissingPlayer()
	{
		var service = new StonespearReachApRewardService();

		var result = service.ApplyFinalApReward(null, points: 67_001, bossKilled: true);

		Assert.Equal(StonespearReachApRewardStatus.MissingPlayer, result.Status);
		Assert.Equal(1, result.Rank);
		Assert.Equal(6_700, result.FinalAp);
		Assert.Null(result.AbyssPointsPlan);
	}

	private static Player CreatePlayer(int objectId, int currentAp)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 65,
			IsOnline = true,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = currentAp },
			Position = new WorldPosition(301210000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}
}
