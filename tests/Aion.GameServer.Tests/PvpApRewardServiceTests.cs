using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PvpApRewardServiceTests
{
	[Fact]
	public void ApplyMemberApReward_CalculatesConfiguredRateAndAddsApThroughPlanner()
	{
		var service = new PvpApRewardService(
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApPvpGainRates = [1f, 1.5f],
				},
			});
		var member = CreatePlayer(objectId: 1600, level: 18, rank: 3, ap: 1_000, membership: 1);
		var victim = CreatePlayer(objectId: 2600, level: 20, rank: 5, ap: 2_000);

		var result = service.ApplyMemberApReward(
			member,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true,
			apWinMultiplier: 2f,
			apBoostStat: 125);

		Assert.Equal(PvpMemberApRewardStatus.Applied, result.Status);
		Assert.Equal(member.ObjectId, result.ObjectId);
		Assert.Equal(victim.ObjectId, result.VictimObjectId);
		Assert.Equal(523, result.BaseRewardAp);
		Assert.Equal(262, result.RewardPerMember);
		Assert.Equal(491, result.MemberApGain);
		Assert.Equal(1_000, result.PreviousAp);
		Assert.Equal(1_491, result.CurrentAp);
		Assert.Equal(1_491, member.AbyssRank.Ap);
		Assert.True(result.UnderDailyKillLimit);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(491, result.AbyssPointsPlan.Added);
		Assert.NotNull(result.AbyssPointsPlan.SiegeCallback);
		Assert.Equal(member.ObjectId, result.AbyssPointsPlan.SiegeCallback.PlayerObjectId);
		Assert.Equal(victim.ObjectId, result.AbyssPointsPlan.SiegeCallback.SourceObjectId);
		Assert.Equal(491, result.AbyssPointsPlan.SiegeCallback.AbyssPoints);
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
	public void ApplyVictimApLoss_CalculatesConfiguredRateAndRemovesDamageShare()
	{
		var service = new PvpApRewardService(
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApPvpLossRates = [1f, 1.5f],
				},
			});
		var victim = CreatePlayer(objectId: 2601, level: 20, rank: 5, ap: 1_000, membership: 1);
		var winner = CreatePlayer(objectId: 1601, level: 24, rank: 3, ap: 500);

		var result = service.ApplyVictimApLoss(
			victim,
			winner,
			apRelevantDamage: 600,
			totalDamage: 1_000);

		Assert.Equal(PvpVictimApLossStatus.Applied, result.Status);
		Assert.Equal(victim.ObjectId, result.ObjectId);
		Assert.Equal(winner.ObjectId, result.WinnerObjectId);
		Assert.Equal(101, result.BaseLossAp);
		Assert.Equal(151, result.RatedLossAp);
		Assert.Equal(90, result.ActualLossAp);
		Assert.Equal(1_000, result.PreviousAp);
		Assert.Equal(910, result.CurrentAp);
		Assert.Equal(910, victim.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(-90, result.AbyssPointsPlan.Added);
		Assert.Collection(
			result.AbyssPointsPlan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300965, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Theory]
	[InlineData(5, 20, 3, 22, 523)]
	[InlineData(5, 20, 3, 25, 52)]
	[InlineData(5, 20, 3, 16, 680)]
	[InlineData(5, 20, 7, 24, 306)]
	public void CalculatePvpApGained_MatchesJavaLevelAndRankPenalties(
		int defeatedRank,
		int defeatedLevel,
		int winnerRank,
		int maxLevel,
		int expected)
	{
		var reward = PvpApRewardService.CalculatePvpApGained(defeatedRank, defeatedLevel, winnerRank, maxLevel);

		Assert.Equal(expected, reward);
	}

	[Theory]
	[InlineData(5, 20, 22, 156)]
	[InlineData(5, 20, 23, 133)]
	[InlineData(5, 20, 24, 101)]
	[InlineData(5, 20, 25, 16)]
	public void CalculatePvpApLost_MatchesJavaLevelPenalties(
		int defeatedRank,
		int defeatedLevel,
		int winnerLevel,
		int expected)
	{
		var loss = PvpApRewardService.CalculatePvpApLost(defeatedRank, defeatedLevel, winnerLevel);

		Assert.Equal(expected, loss);
	}

	[Fact]
	public void CalculateMemberApGain_UsesJavaMinimumAndRateFallbacks()
	{
		var capped = PvpApRewardService.CalculateMemberApGain(
			membershipLevel: 1,
			rewardPerMember: 250,
			underDailyKillLimit: false,
			apBoostStat: 200,
			apPvpGainRates: [1f, 3f]);
		var zeroReward = PvpApRewardService.CalculateMemberApGain(
			membershipLevel: 1,
			rewardPerMember: 0,
			underDailyKillLimit: true,
			apBoostStat: 200,
			apPvpGainRates: [1f, 3f]);
		var emptyRates = PvpApRewardService.CalculateMemberApGain(
			membershipLevel: 7,
			rewardPerMember: 250,
			underDailyKillLimit: true,
			apBoostStat: 125,
			apPvpGainRates: []);

		Assert.Equal(1, capped);
		Assert.Equal(1, zeroReward);
		Assert.Equal(312, emptyRates);
	}

	[Fact]
	public void ApplyVictimApLoss_SkipsMissingAndNonRelevantDamage()
	{
		var service = new PvpApRewardService();
		var victim = CreatePlayer(objectId: 2602, level: 20, rank: 5, ap: 1_000);
		var winner = CreatePlayer(objectId: 1602, level: 24, rank: 3, ap: 500);

		var missingVictim = service.ApplyVictimApLoss(null, winner, apRelevantDamage: 10, totalDamage: 100);
		var missingWinner = service.ApplyVictimApLoss(victim, null, apRelevantDamage: 10, totalDamage: 100);
		var noRelevantDamage = service.ApplyVictimApLoss(victim, winner, apRelevantDamage: 0, totalDamage: 100);
		var noTotalDamage = service.ApplyVictimApLoss(victim, winner, apRelevantDamage: 10, totalDamage: 0);

		Assert.Equal(PvpVictimApLossStatus.MissingVictim, missingVictim.Status);
		Assert.Equal(PvpVictimApLossStatus.MissingWinner, missingWinner.Status);
		Assert.Equal(PvpVictimApLossStatus.NoRelevantDamage, noRelevantDamage.Status);
		Assert.Equal(PvpVictimApLossStatus.NoRelevantDamage, noTotalDamage.Status);
		Assert.Null(missingVictim.AbyssPointsPlan);
		Assert.Null(missingWinner.AbyssPointsPlan);
		Assert.Null(noRelevantDamage.AbyssPointsPlan);
		Assert.Null(noTotalDamage.AbyssPointsPlan);
		Assert.Equal(1_000, victim.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyMemberApReward_SkipsMissingInputsAndNoEligibleMembers()
	{
		var service = new PvpApRewardService();
		var member = CreatePlayer(objectId: 1603, level: 18, rank: 3, ap: 1_000);
		var victim = CreatePlayer(objectId: 2603, level: 20, rank: 5, ap: 2_000);

		var missingMember = service.ApplyMemberApReward(
			null,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true);
		var missingVictim = service.ApplyMemberApReward(
			member,
			null,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 2,
			underDailyKillLimit: true);
		var noEligibleMembers = service.ApplyMemberApReward(
			member,
			victim,
			maxRank: 3,
			maxLevel: 22,
			groupDamagePercentage: 0.5f,
			eligibleMemberCount: 0,
			underDailyKillLimit: true);

		Assert.Equal(PvpMemberApRewardStatus.MissingMember, missingMember.Status);
		Assert.Equal(PvpMemberApRewardStatus.MissingVictim, missingVictim.Status);
		Assert.Equal(PvpMemberApRewardStatus.NoEligibleMembers, noEligibleMembers.Status);
		Assert.Null(missingMember.AbyssPointsPlan);
		Assert.Null(missingVictim.AbyssPointsPlan);
		Assert.Null(noEligibleMembers.AbyssPointsPlan);
		Assert.Equal(1_000, member.AbyssRank.Ap);
	}

	private static Player CreatePlayer(
		int objectId,
		int level,
		int rank,
		int ap,
		byte membership = 0)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = level,
			IsOnline = true,
			AccountMembership = membership,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = ap, Rank = rank, MaxRank = rank },
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}
}
