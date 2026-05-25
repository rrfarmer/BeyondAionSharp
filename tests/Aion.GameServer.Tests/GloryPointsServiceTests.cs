using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class GloryPointsServiceTests
{
	[Fact]
	public void AddGp_AddsPositiveGpDailyWeeklyStatsAndRankPacketLikeJava()
	{
		var player = CreatePlayer(gp: 100, dailyGp: 5, weeklyGp: 10);

		var plan = GloryPointsService.AddGp(player, player.ObjectId, amount: 50);

		Assert.Equal(GloryPointsAddStatus.Applied, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(player.ObjectId, plan.ObjectId);
		Assert.Equal(50, plan.Amount);
		Assert.Equal(50, plan.Added);
		Assert.Equal(100, plan.PreviousGp);
		Assert.Equal(150, player.AbyssRank.Gp);
		Assert.Equal(55, player.AbyssRank.DailyGp);
		Assert.Equal(60, player.AbyssRank.WeeklyGp);
		Assert.True(plan.AddsDailyWeeklyStats);
		Assert.False(plan.RequiresOfflineDaoUpdate);
		Assert.Collection(
			plan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1402081, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public void AddGp_SubtractsGpClampsAtZeroAndSkipsDailyWeeklyStatsLikeJava()
	{
		var player = CreatePlayer(gp: 30, dailyGp: 5, weeklyGp: 10);

		var plan = GloryPointsService.AddGp(player, player.ObjectId, amount: -100);

		Assert.Equal(GloryPointsAddStatus.Applied, plan.Status);
		Assert.Equal(-100, plan.Amount);
		Assert.Equal(-30, plan.Added);
		Assert.Equal(0, player.AbyssRank.Gp);
		Assert.Equal(5, player.AbyssRank.DailyGp);
		Assert.Equal(10, player.AbyssRank.WeeklyGp);
		Assert.False(plan.AddsDailyWeeklyStats);
		Assert.Collection(
			plan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1402219, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	[Fact]
	public void AddGp_StillSendsLossZeroWhenNegativeAmountDoesNotChangeCurrentGp()
	{
		var player = CreatePlayer(gp: 0, dailyGp: 5, weeklyGp: 10);

		var plan = GloryPointsService.AddGp(player, player.ObjectId, amount: -100);

		Assert.Equal(GloryPointsAddStatus.Applied, plan.Status);
		Assert.Equal(0, plan.Added);
		Assert.Equal(0, player.AbyssRank.Gp);
		Assert.Single(plan.PlayerPackets);
		var message = Assert.IsType<SmSystemMessage>(plan.PlayerPackets[0]);
		Assert.Equal(1402219, message.MessageId);
	}

	[Fact]
	public void CreateAddGpPlan_RecordsOfflineDaoBranchAndZeroGuard()
	{
		var offlineGain = GloryPointsService.CreateAddGpPlan(player: null, playerObjectId: 1402, amount: 25);
		var offlineLoss = GloryPointsService.CreateAddGpPlan(player: null, playerObjectId: 1403, amount: -25);
		var zero = GloryPointsService.CreateAddGpPlan(CreatePlayer(), playerObjectId: 1404, amount: 0);

		Assert.Equal(GloryPointsAddStatus.OfflineDaoUpdateRequired, offlineGain.Status);
		Assert.True(offlineGain.RequiresOfflineDaoUpdate);
		Assert.True(offlineGain.AddsDailyWeeklyStats);
		Assert.Empty(offlineGain.PlayerPackets);

		Assert.Equal(GloryPointsAddStatus.OfflineDaoUpdateRequired, offlineLoss.Status);
		Assert.True(offlineLoss.RequiresOfflineDaoUpdate);
		Assert.False(offlineLoss.AddsDailyWeeklyStats);
		Assert.Empty(offlineLoss.PlayerPackets);

		Assert.Equal(GloryPointsAddStatus.NoReward, zero.Status);
		Assert.Empty(zero.PlayerPackets);
	}

	[Fact]
	public void AddGp_PreservesJavaIntOverflowShape()
	{
		var player = CreatePlayer(gp: int.MaxValue, dailyGp: int.MaxValue, weeklyGp: int.MaxValue);

		var plan = GloryPointsService.AddGp(player, player.ObjectId, amount: 1);

		Assert.Equal(-int.MaxValue, plan.Added);
		Assert.Equal(0, player.AbyssRank.Gp);
		Assert.Equal(int.MinValue, player.AbyssRank.DailyGp);
		Assert.Equal(int.MinValue, player.AbyssRank.WeeklyGp);
		Assert.Collection(
			plan.PlayerPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1402081, message.MessageId);
			},
			packet => Assert.IsType<SmAbyssRank>(packet));
	}

	private static Player CreatePlayer(
		int gp = 0,
		int dailyGp = 0,
		int weeklyGp = 0)
	{
		return new Player
		{
			ObjectId = 1401,
			AbyssRank = PlayerAbyssRank.Default() with
			{
				Gp = gp,
				DailyGp = dailyGp,
				WeeklyGp = weeklyGp,
			},
		};
	}
}
