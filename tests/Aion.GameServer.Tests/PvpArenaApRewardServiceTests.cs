using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PvpArenaApRewardServiceTests
{
	[Fact]
	public void CalculateApReward_AppliesJavaRankScorePoolsAndConfiguredRate()
	{
		var service = new PvpArenaApRewardService(
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					PvpArenaChaosRewardRates = [1f, 1.5f],
				},
			});

		var reward = service.CalculateApReward(
			PvpArenaKind.Chaos,
			membershipLevel: 1,
			baseApPerPlayer: 1_000,
			baseAp: 200,
			playerCount: 4,
			scorePoints: 2_500,
			totalPoints: 10_000,
			rankRewardRate: 0.16f);

		Assert.Equal(200, reward.BaseCount);
		Assert.Equal(672, reward.RankingCount);
		Assert.Equal(450, reward.ScoreCount);
		Assert.Equal(1_322, reward.TotalCount);
	}

	[Fact]
	public void ApplyApReward_AddsPositiveArenaApThroughPlanner()
	{
		var service = new PvpArenaApRewardService();
		var player = CreatePlayer(objectId: 1600, currentAp: 500);
		var reward = new PvpArenaApRewardItem(BaseCount: 200, RankingCount: 672, ScoreCount: 450);

		var result = service.ApplyApReward(player, reward);

		Assert.Equal(PvpArenaApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(reward, result.Reward);
		Assert.Equal(500, result.PreviousAp);
		Assert.Equal(1_822, result.CurrentAp);
		Assert.Equal(1_822, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(1_322, result.AbyssPointsPlan.Added);
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
	public void ApplyApReward_SkipsMissingPlayerAndZeroTotalReward()
	{
		var service = new PvpArenaApRewardService();
		var player = CreatePlayer(objectId: 1601, currentAp: 800);
		var reward = new PvpArenaApRewardItem(BaseCount: 0, RankingCount: 0, ScoreCount: 0);

		var missingPlayer = service.ApplyApReward(null, new PvpArenaApRewardItem(1, 2, 3));
		var noReward = service.ApplyApReward(player, reward);

		Assert.Equal(PvpArenaApRewardStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(PvpArenaApRewardStatus.NoApReward, noReward.Status);
		Assert.Null(missingPlayer.AbyssPointsPlan);
		Assert.Null(noReward.AbyssPointsPlan);
		Assert.Equal(800, player.AbyssRank.Ap);
	}

	[Fact]
	public void CalculateIndividualApReward_MatchesJavaHarmonyGroupMathRound()
	{
		var groupReward = new PvpArenaApRewardItem(BaseCount: 200, RankingCount: 900, ScoreCount: 301);

		var individual = PvpArenaApRewardService.CalculateIndividualApReward(groupReward, groupSize: 2, configRate: 1.5f);
		var noGroup = PvpArenaApRewardService.CalculateIndividualApReward(groupReward, groupSize: 0, configRate: 1.5f);

		Assert.Equal(150, individual.BaseCount);
		Assert.Equal(675, individual.RankingCount);
		Assert.Equal(226, individual.ScoreCount);
		Assert.Equal(1_051, individual.TotalCount);
		Assert.Equal(0, noGroup.TotalCount);
	}

	[Fact]
	public void SelectConfiguredRewardRate_MatchesJavaMembershipFallbacks()
	{
		var service = new PvpArenaApRewardService(
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					PvpArenaDisciplineRewardRates = [1f, 1.25f],
					PvpArenaGloryRewardRates = [],
				},
			});

		var clamped = service.SelectConfiguredRewardRate(PvpArenaKind.Discipline, membershipLevel: 7);
		var emptyFallback = service.SelectConfiguredRewardRate(PvpArenaKind.Glory, membershipLevel: 1);
		var overrideRate = service.SelectConfiguredRewardRate(PvpArenaKind.Harmony, membershipLevel: 1, overrideRates: [1f, 2.5f]);

		Assert.Equal(1.25f, clamped);
		Assert.Equal(1f, emptyFallback);
		Assert.Equal(2.5f, overrideRate);
	}

	[Fact]
	public void CalculateApReward_GuardsInvalidScoreProjectionInputs()
	{
		var noPlayers = PvpArenaApRewardService.CalculateApReward(
			baseApPerPlayer: 1_000,
			baseAp: 200,
			playerCount: 0,
			scorePoints: 2_500,
			totalPoints: 10_000,
			rankRewardRate: 0.16f,
			configRate: 1.5f);
		var noTotalPoints = PvpArenaApRewardService.CalculateApReward(
			baseApPerPlayer: 1_000,
			baseAp: 200,
			playerCount: 4,
			scorePoints: 2_500,
			totalPoints: 0,
			rankRewardRate: 0.16f,
			configRate: 1.5f);

		Assert.Equal(new PvpArenaApRewardItem(200, 0, 0), noPlayers);
		Assert.Equal(new PvpArenaApRewardItem(200, 0, 0), noTotalPoints);
	}

	private static Player CreatePlayer(int objectId, int currentAp)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 60,
			IsOnline = true,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = currentAp },
			Position = new WorldPosition(300350000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}
}
