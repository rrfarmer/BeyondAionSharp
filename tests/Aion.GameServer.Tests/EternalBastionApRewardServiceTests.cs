using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class EternalBastionApRewardServiceTests
{
	[Theory]
	[InlineData(90_000, 1, 35_000)]
	[InlineData(82_000, 2, 25_000)]
	[InlineData(60_000, 3, 15_000)]
	[InlineData(30_000, 4, 11_000)]
	[InlineData(5_000, 5, 7_000)]
	[InlineData(4_999, 8, 0)]
	public void CalculateFinalRankAndAp_MatchesJavaThresholds(int points, int expectedRank, int expectedAp)
	{
		var rank = EternalBastionApRewardService.CalculateFinalRank(points);
		var finalAp = EternalBastionApRewardService.GetFinalAp(rank);

		Assert.Equal(expectedRank, rank);
		Assert.Equal(expectedAp, finalAp);
	}

	[Fact]
	public void ApplyFinalApReward_AddsRankBasedFinalApThroughPlanner()
	{
		var service = new EternalBastionApRewardService();
		var player = CreatePlayer(objectId: 1800, currentAp: 2_000);

		var result = service.ApplyFinalApReward(player, points: 90_000);

		Assert.Equal(EternalBastionApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(90_000, result.Points);
		Assert.Equal(1, result.Rank);
		Assert.Equal(35_000, result.FinalAp);
		Assert.Equal(2_000, result.PreviousAp);
		Assert.Equal(37_000, result.CurrentAp);
		Assert.Equal(37_000, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(35_000, result.AbyssPointsPlan.Added);
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
	public void ApplyFinalApReward_RoutesNoRankApLikeJavaDistributeRewards()
	{
		var service = new EternalBastionApRewardService();
		var player = CreatePlayer(objectId: 1801, currentAp: 3_000);

		var result = service.ApplyFinalApReward(player, points: 4_999);

		Assert.Equal(EternalBastionApRewardStatus.Applied, result.Status);
		Assert.Equal(8, result.Rank);
		Assert.Equal(0, result.FinalAp);
		Assert.Equal(3_000, result.PreviousAp);
		Assert.Equal(3_000, result.CurrentAp);
		Assert.Equal(3_000, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(0, result.AbyssPointsPlan.Added);
		Assert.NotEmpty(result.AbyssPointsPlan.PlayerPackets);
		var message = Assert.IsType<SmSystemMessage>(result.AbyssPointsPlan.PlayerPackets[0]);
		Assert.Equal(1320000, message.MessageId);
	}

	[Fact]
	public void ApplyFinalApReward_SkipsMissingPlayer()
	{
		var service = new EternalBastionApRewardService();

		var result = service.ApplyFinalApReward(null, points: 82_000);

		Assert.Equal(EternalBastionApRewardStatus.MissingPlayer, result.Status);
		Assert.Equal(2, result.Rank);
		Assert.Equal(25_000, result.FinalAp);
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
			Position = new WorldPosition(301220000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}
}
