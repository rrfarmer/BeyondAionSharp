using Aion.Commons.Database;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IAbyssRankRepository
{
	Task<bool> AddGpAsync(
		int playerObjectId,
		int additionalGp,
		bool modifyStats,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyAbyssRankRepository : IAbyssRankRepository
{
	public Task<bool> AddGpAsync(
		int playerObjectId,
		int additionalGp,
		bool modifyStats,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlAbyssRankRepository : IAbyssRankRepository
{
	private readonly ILogger<MySqlAbyssRankRepository> _logger;

	public MySqlAbyssRankRepository(ILogger<MySqlAbyssRankRepository> logger)
	{
		_logger = logger;
	}

	public async Task<bool> AddGpAsync(
		int playerObjectId,
		int additionalGp,
		bool modifyStats,
		CancellationToken cancellationToken = default)
	{
		var plan = AbyssRankGpUpdatePlan.Create(playerObjectId, additionalGp, modifyStats);
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = plan.Sql;
			foreach (var parameter in plan.Parameters)
				command.Parameters.Add(new MySqlParameter { Value = parameter });

			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Couldn't increase {AdditionalGp} GP for player {PlayerObjectId}", additionalGp, playerObjectId);
			return false;
		}
	}
}

public sealed record AbyssRankGpUpdatePlan(
	int PlayerObjectId,
	int AdditionalGp,
	bool ModifyStats,
	string Sql,
	IReadOnlyList<int> Parameters,
	string JavaSource)
{
	private const string IncreaseGpWithStatsSql =
		"UPDATE abyss_rank SET gp = gp + ?, daily_gp = daily_gp + ?, weekly_gp = weekly_gp + ? WHERE player_id = ?";
	private const string IncreaseGpSql =
		"UPDATE abyss_rank SET gp = GREATEST(gp + ?, 0) WHERE player_id = ?";

	public static AbyssRankGpUpdatePlan Create(int playerObjectId, int additionalGp, bool modifyStats)
	{
		// Java parity: dao/AbyssRankDAO.addGp chooses INCREASE_GP_QUERY_WITH_STATS
		// for positive GP stats and INCREASE_GP_QUERY for current-GP-only changes.
		return modifyStats
			? new AbyssRankGpUpdatePlan(
				playerObjectId,
				additionalGp,
				modifyStats,
				IncreaseGpWithStatsSql,
				[additionalGp, additionalGp, additionalGp, playerObjectId],
				"game-server/src/com/aionemu/gameserver/dao/AbyssRankDAO.java#addGp")
			: new AbyssRankGpUpdatePlan(
				playerObjectId,
				additionalGp,
				modifyStats,
				IncreaseGpSql,
				[additionalGp, playerObjectId],
				"game-server/src/com/aionemu/gameserver/dao/AbyssRankDAO.java#addGp");
	}
}
