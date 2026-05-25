using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcTeamApRewardServiceTests
{
	[Fact]
	public void ApplyMemberApRewardFromNpcStats_CalculatesJavaTeamShareAndAddsAp()
	{
		var service = new WorldNpcTeamApRewardService();
		var player = CreatePlayer(objectId: 1500, level: 10, currentAp: 1_000);
		player.AccountMembership = 1;
		var npc = CreateNpc(objectId: 2500, level: 12, rating: "ELITE");

		var result = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 0.5f,
			instanceApMultiplier: 2f,
			eligiblePlayerCount: 3,
			shouldRewardAp: true,
			apPveRates: [1f, 1.5f],
			apBoostStat: 100);

		Assert.Equal(WorldNpcTeamApRewardStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(npc.ObjectId, result.NpcObjectId);
		Assert.Equal(90, result.CalculatedAp);
		Assert.Equal(30, result.RewardAp);
		Assert.Equal(1_000, result.PreviousAp);
		Assert.Equal(1_030, result.CurrentAp);
		Assert.Equal(1_030, player.AbyssRank.Ap);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.Equal(30, result.AbyssPointsPlan.Added);
		Assert.Null(result.AbyssPointsPlan.SiegeCallback);
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
	public void ApplyMemberApRewardFromNpcStats_UsesConfiguredApPveRatesWhenNoOverrideIsSupplied()
	{
		var service = new WorldNpcTeamApRewardService(
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					ApPveRates = [1f, 1.25f],
				},
			});
		var player = CreatePlayer(objectId: 1501, level: 10, currentAp: 600);
		player.AccountMembership = 1;
		var npc = CreateNpc(objectId: 2501, level: 12, rating: "ELITE");

		var result = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 5,
			shouldRewardAp: true,
			apBoostStat: 100);

		Assert.Equal(WorldNpcTeamApRewardStatus.Applied, result.Status);
		Assert.Equal(75, result.CalculatedAp);
		Assert.Equal(15, result.RewardAp);
		Assert.Equal(615, result.CurrentAp);
		Assert.Equal(615, player.AbyssRank.Ap);
	}

	[Fact]
	public void CalculateTeamMemberApReward_MatchesJavaFloatNarrowingAndGroupDivision()
	{
		var roundedDown = WorldNpcTeamApRewardService.CalculateTeamMemberApReward(
			calculatedPveAp: 95,
			damagePercent: 0.5f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 3);
		var nanDamage = WorldNpcTeamApRewardService.CalculateTeamMemberApReward(
			calculatedPveAp: 95,
			damagePercent: float.NaN,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 3);
		var noEligible = WorldNpcTeamApRewardService.CalculateTeamMemberApReward(
			calculatedPveAp: 95,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 0);

		Assert.Equal(15, roundedDown);
		Assert.Equal(0, nanDamage);
		Assert.Equal(0, noEligible);
	}

	[Fact]
	public void ApplyMemberApRewardFromNpcStats_SkipsAiDeniedMentorSuppressedAndBelowMinimum()
	{
		var service = new WorldNpcTeamApRewardService();
		var player = CreatePlayer(objectId: 1502, level: 10, currentAp: 200);
		var npc = CreateNpc(objectId: 2502, level: 12, rating: "ELITE");

		var denied = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 2,
			shouldRewardAp: false);
		var mentorSuppressed = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 2,
			shouldRewardAp: true,
			suppressApForMentorGroup: true);
		var belowMinimum = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 0.1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 10,
			shouldRewardAp: true,
			apPveRates: [1f],
			apBoostStat: 100);

		Assert.Equal(WorldNpcTeamApRewardStatus.ApRewardDenied, denied.Status);
		Assert.Equal(WorldNpcTeamApRewardStatus.MentorGroupApSuppressed, mentorSuppressed.Status);
		Assert.Equal(WorldNpcTeamApRewardStatus.NoApReward, belowMinimum.Status);
		Assert.Null(denied.AbyssPointsPlan);
		Assert.Null(mentorSuppressed.AbyssPointsPlan);
		Assert.Null(belowMinimum.AbyssPointsPlan);
		Assert.Equal(200, player.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyMemberApRewardFromNpcStats_SkipsMissingDeadAndNoEligibleTargets()
	{
		var service = new WorldNpcTeamApRewardService();
		var npc = CreateNpc(objectId: 2503, level: 12, rating: "ELITE");
		var player = CreatePlayer(objectId: 1503, level: 10, currentAp: 300);
		var deadPlayer = CreatePlayer(objectId: 1504, level: 10, currentAp: 400, currentHp: 0);

		var missingMember = service.ApplyMemberApRewardFromNpcStats(
			null,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 1,
			shouldRewardAp: true);
		var missingNpc = service.ApplyMemberApRewardFromNpcStats(
			player,
			null,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 1,
			shouldRewardAp: true);
		var noEligible = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 0,
			shouldRewardAp: true);
		var playerDead = service.ApplyMemberApRewardFromNpcStats(
			deadPlayer,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 2,
			shouldRewardAp: true);

		Assert.Equal(WorldNpcTeamApRewardStatus.MissingMember, missingMember.Status);
		Assert.Equal(WorldNpcTeamApRewardStatus.MissingNpc, missingNpc.Status);
		Assert.Equal(WorldNpcTeamApRewardStatus.NoEligiblePlayers, noEligible.Status);
		Assert.Equal(WorldNpcTeamApRewardStatus.PlayerDead, playerDead.Status);
		Assert.Null(missingMember.AbyssPointsPlan);
		Assert.Null(missingNpc.AbyssPointsPlan);
		Assert.Null(noEligible.AbyssPointsPlan);
		Assert.Null(playerDead.AbyssPointsPlan);
		Assert.Equal(300, player.AbyssRank.Ap);
		Assert.Equal(400, deadPlayer.AbyssRank.Ap);
	}

	[Fact]
	public void ApplyMemberApRewardFromNpcStats_CreatesSiegeCallbackForNonPeaceSiegeNpc()
	{
		var service = new WorldNpcTeamApRewardService();
		var player = CreatePlayer(objectId: 1505, level: 10, currentAp: 500);
		var npc = CreateNpc(objectId: 2505, level: 12, rating: "ELITE");

		var result = service.ApplyMemberApRewardFromNpcStats(
			player,
			npc,
			damagePercent: 1f,
			instanceApMultiplier: 1f,
			eligiblePlayerCount: 4,
			shouldRewardAp: true,
			sourceIsSiegeNpc: true,
			sourceSiegeNpcPeace: false);

		Assert.Equal(WorldNpcTeamApRewardStatus.Applied, result.Status);
		Assert.NotNull(result.AbyssPointsPlan?.SiegeCallback);
		Assert.Equal(player.ObjectId, result.AbyssPointsPlan.SiegeCallback.PlayerObjectId);
		Assert.Equal(npc.ObjectId, result.AbyssPointsPlan.SiegeCallback.SourceObjectId);
		Assert.Equal(result.RewardAp, result.AbyssPointsPlan.SiegeCallback.AbyssPoints);
	}

	private static Player CreatePlayer(
		int objectId,
		int level,
		int currentAp,
		int currentHp = 100)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = level,
			Dp = 100,
			IsOnline = true,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = currentAp },
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(currentHp, 100, 100),
		};
	}

	private static WorldNpc CreateNpc(int objectId, int level, string rating)
	{
		var template = new NpcTemplateSummary(
			TemplateId: 8300 + level,
			Name: "Training Target",
			NameId: 8300 + level,
			Level: level,
			Rank: "NORMAL",
			Rating: rating,
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC");
		return new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			new WorldPosition(210010000, 15, 25, 30, 0));
	}
}
