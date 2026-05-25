using Aion.GameServer.Data;

namespace Aion.GameServer.Tests;

public sealed class AbyssRankRepositoryTests
{
	[Fact]
	public void AbyssRankGpUpdatePlan_UsesJavaPositiveStatsSqlAndParameterOrder()
	{
		var plan = AbyssRankGpUpdatePlan.Create(playerObjectId: 4100, additionalGp: 75, modifyStats: true);

		Assert.Equal(4100, plan.PlayerObjectId);
		Assert.Equal(75, plan.AdditionalGp);
		Assert.True(plan.ModifyStats);
		Assert.Equal(
			"UPDATE abyss_rank SET gp = gp + ?, daily_gp = daily_gp + ?, weekly_gp = weekly_gp + ? WHERE player_id = ?",
			plan.Sql);
		Assert.Equal([75, 75, 75, 4100], plan.Parameters);
		Assert.Equal("game-server/src/com/aionemu/gameserver/dao/AbyssRankDAO.java#addGp", plan.JavaSource);
	}

	[Theory]
	[InlineData(-30)]
	[InlineData(0)]
	public void AbyssRankGpUpdatePlan_UsesJavaCurrentGpClampSqlWhenStatsAreNotModified(int additionalGp)
	{
		var plan = AbyssRankGpUpdatePlan.Create(playerObjectId: 4101, additionalGp, modifyStats: false);

		Assert.Equal(4101, plan.PlayerObjectId);
		Assert.Equal(additionalGp, plan.AdditionalGp);
		Assert.False(plan.ModifyStats);
		Assert.Equal("UPDATE abyss_rank SET gp = GREATEST(gp + ?, 0) WHERE player_id = ?", plan.Sql);
		Assert.Equal([additionalGp, 4101], plan.Parameters);
	}

	[Fact]
	public async Task EmptyAbyssRankRepository_ReportsUnavailableMutationBoundary()
	{
		var repository = new EmptyAbyssRankRepository();

		var result = await repository.AddGpAsync(playerObjectId: 4102, additionalGp: 10, modifyStats: true);

		Assert.False(result);
	}
}
