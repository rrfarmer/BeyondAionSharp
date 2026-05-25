using Aion.GameServer.Data;

namespace Aion.GameServer.Tests;

public sealed class CustomLevelRewardRepositoryTests
{
	[Theory]
	[InlineData(CustomLevelRewardReceiptKind.Bonus, "SELECT `receiving_player` FROM `bonus_packs` WHERE `account_id`=?", "BonusPackDAO.java#loadReceivingPlayer")]
	[InlineData(CustomLevelRewardReceiptKind.Faction, "SELECT `receiving_player` FROM `faction_packs` WHERE `account_id`=?", "FactionPackDAO.java#loadReceivingPlayer")]
	public void CreateLoad_UsesJavaSelectSqlAndAccountParameter(
		CustomLevelRewardReceiptKind kind,
		string expectedSql,
		string expectedJavaSource)
	{
		var plan = CustomLevelRewardReceiptRepositoryPlan.CreateLoad(kind, accountId: 3301);

		Assert.Equal(kind, plan.Kind);
		Assert.Equal(CustomLevelRewardReceiptRepositoryAction.LoadReceivingPlayer, plan.Action);
		Assert.Equal(3301, plan.AccountId);
		Assert.Null(plan.PlayerObjectId);
		Assert.Equal(expectedSql, plan.Sql);
		Assert.Equal([3301], plan.Parameters);
		Assert.EndsWith(expectedJavaSource, plan.JavaSource, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(CustomLevelRewardReceiptKind.Bonus, "REPLACE INTO `bonus_packs` (`account_id`, `receiving_player`) VALUES (?,?)", "BonusPackDAO.java#storeReceivingPlayer")]
	[InlineData(CustomLevelRewardReceiptKind.Faction, "REPLACE INTO `faction_packs` (`account_id`, `receiving_player`) VALUES (?,?)", "FactionPackDAO.java#storeReceivingPlayer")]
	public void CreateStore_UsesJavaReplaceSqlAndParameterOrder(
		CustomLevelRewardReceiptKind kind,
		string expectedSql,
		string expectedJavaSource)
	{
		var plan = CustomLevelRewardReceiptRepositoryPlan.CreateStore(kind, accountId: 3301, playerObjectId: 4701);

		Assert.Equal(kind, plan.Kind);
		Assert.Equal(CustomLevelRewardReceiptRepositoryAction.StoreReceivingPlayer, plan.Action);
		Assert.Equal(3301, plan.AccountId);
		Assert.Equal(4701, plan.PlayerObjectId);
		Assert.Equal(expectedSql, plan.Sql);
		Assert.Equal([3301, 4701], plan.Parameters);
		Assert.EndsWith(expectedJavaSource, plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public async Task EmptyRepository_ReportsUnavailableJavaSafeFallbacks()
	{
		var repository = new EmptyCustomLevelRewardRepository();

		Assert.Equal(
			int.MaxValue,
			await repository.LoadReceivingPlayerAsync(CustomLevelRewardReceiptKind.Bonus, accountId: 3301));
		Assert.False(
			await repository.StoreReceivingPlayerAsync(CustomLevelRewardReceiptKind.Faction, accountId: 3301, playerObjectId: 4701));
	}
}
