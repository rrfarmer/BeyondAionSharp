using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PvpInstanceApRewardServiceTests
{
	[Fact]
	public void ApplyApReward_AppliesConfiguredDredgionRateAndAddsApThroughPlanner()
	{
		var service = new PvpInstanceApRewardService(
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApDredgionRates = [1f, 1.5f],
				},
			});
		var player = CreatePlayer(objectId: 1700, ap: 1_000, membership: 1);

		var result = service.ApplyApReward(player, baseAp: 4_500, bonusAp: 500);

		Assert.Equal(PvpInstanceApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(4_500, result.BaseAp);
		Assert.Equal(500, result.BonusAp);
		Assert.Equal(5_000, result.TotalAp);
		Assert.Equal(7_500, result.AppliedAp);
		Assert.Equal(1_000, result.PreviousAp);
		Assert.Equal(8_500, result.CurrentAp);
		Assert.Equal(8_500, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(7_500, result.AbyssPointsPlan.Added);
		Assert.Collection(
			result.AbyssPointsPlan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1320000, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Theory]
	[InlineData(true, false, 4_500, 2_500, 3_750, 1_800, 0, 4_500, 600)]
	[InlineData(false, false, 4_500, 2_500, 3_750, 1_800, 0, 2_500, 300)]
	[InlineData(false, true, 4_500, 2_500, 3_750, 1_800, 0, 3_750, 300)]
	[InlineData(true, false, 4_500, 2_500, 3_750, 1_800, 3_850, 8_350, 600)]
	public void CalculateFactionApReward_MatchesJavaDredgionAndBasicPvpRewardBranches(
		bool playerRaceWon,
		bool draw,
		int winnerApReward,
		int loserApReward,
		int drawApReward,
		int scorePoints,
		int winnerBonusAp,
		int expectedBaseAp,
		int expectedBonusAp)
	{
		var reward = PvpInstanceApRewardService.CalculateFactionApReward(
			winnerApReward,
			loserApReward,
			drawApReward,
			scorePoints,
			playerRaceWon,
			draw,
			winnerBonusAp);

		Assert.Equal(expectedBaseAp, reward.BaseAp);
		Assert.Equal(expectedBonusAp, reward.BonusAp);
		Assert.Equal(expectedBaseAp + expectedBonusAp, reward.TotalAp);
	}

	[Fact]
	public void ApplyDredgionApRate_MatchesJavaMembershipFallbacksAndOverflowBehavior()
	{
		var clampedMembership = PvpInstanceApRewardService.ApplyDredgionApRate(
			membershipLevel: 7,
			rewardAp: 1_000,
			apDredgionRates: [1f, 1.25f]);
		var emptyRates = PvpInstanceApRewardService.ApplyDredgionApRate(
			membershipLevel: 7,
			rewardAp: 1_000,
			apDredgionRates: []);
		var overflowFallback = PvpInstanceApRewardService.ApplyDredgionApRate(
			membershipLevel: 1,
			rewardAp: int.MaxValue,
			apDredgionRates: [1f, 2f]);

		Assert.Equal(1_250, clampedMembership);
		Assert.Equal(1_000, emptyRates);
		Assert.Equal(int.MaxValue, overflowFallback);
	}

	[Fact]
	public void ApplyApReward_HandlesMissingPlayerAndZeroRewardLikePlannerBoundary()
	{
		var service = new PvpInstanceApRewardService();
		var player = CreatePlayer(objectId: 1701, ap: 1_000);

		var missingPlayer = service.ApplyApReward(null, baseAp: 4_500, bonusAp: 500);
		var zeroReward = service.ApplyApReward(player, baseAp: 0, bonusAp: 0);

		Assert.Equal(PvpInstanceApRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Null(missingPlayer.AbyssPointsPlan);
		Assert.Equal(PvpInstanceApRewardStatus.Applied, zeroReward.Status);
		Assert.Equal(0, zeroReward.TotalAp);
		Assert.Equal(0, zeroReward.AppliedAp);
		Assert.Equal(1_000, zeroReward.CurrentAp);
		Assert.Equal(1_000, player.AbyssRank.Ap);
		Assert.NotNull(zeroReward.AbyssPointsPlan);
		Assert.Equal(0, zeroReward.AbyssPointsPlan.Added);
		Assert.Single(zeroReward.AbyssPointsPlan.PlayerPackets);
		var message = Assert.IsType<SmSystemMessage>(zeroReward.AbyssPointsPlan.PlayerPackets[0]);
		Assert.Equal(1320000, message.MessageId);
	}

	private static Player CreatePlayer(int objectId, int ap, byte membership = 0)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 50,
			IsOnline = true,
			AccountMembership = membership,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = ap },
			Position = new WorldPosition(300110000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}
}
