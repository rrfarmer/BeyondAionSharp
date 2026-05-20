using Aion.Commons.Database;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Data;

public interface IUsedIdRepository
{
	Task<IReadOnlyCollection<int>> LoadUsedIdsAsync(CancellationToken cancellationToken = default);
}

public sealed class EmptyUsedIdRepository : IUsedIdRepository
{
	public Task<IReadOnlyCollection<int>> LoadUsedIdsAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyCollection<int>>(Array.Empty<int>());
	}
}

public sealed class MySqlUsedIdRepository : IUsedIdRepository
{
	private static readonly UsedIdQuery[] UsedIdQueries =
	[
		new("players", "SELECT id FROM players"),
		new("inventory", "SELECT item_unique_id FROM inventory"),
		new("player_registered_items", "SELECT item_unique_id FROM player_registered_items WHERE item_unique_id <> 0"),
		new("legions", "SELECT id FROM legions"),
		new("mail", "SELECT mail_unique_id FROM mail"),
		new("guides", "SELECT guide_id FROM guides"),
		new("houses", "SELECT DISTINCT id FROM houses"),
		new("player_pets", "SELECT id FROM player_pets"),
	];

	private readonly ILogger<MySqlUsedIdRepository> _logger;

	public MySqlUsedIdRepository(ILogger<MySqlUsedIdRepository> logger)
	{
		_logger = logger;
	}

	public async Task<IReadOnlyCollection<int>> LoadUsedIdsAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: utils/idfactory/IDFactory.initializeUsedIds.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);

			var ids = new List<int>();
			foreach (var query in UsedIdQueries)
			{
				var tableCount = await LoadTableIdsAsync(connection, query, ids, cancellationToken);
				_logger.LogDebug("Preloaded {Count} used IDs from {Table}", tableCount, query.TableName);
			}

			return ids;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not preload used game object IDs from the database");
			throw;
		}
	}

	private static async Task<int> LoadTableIdsAsync(
		MySqlConnector.MySqlConnection connection,
		UsedIdQuery query,
		List<int> ids,
		CancellationToken cancellationToken)
	{
		// Java parity: each DAO.getUsedIDs query listed in UsedIdQueries.
		await using var command = connection.CreateCommand();
		command.CommandText = query.Sql;

		var count = 0;
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			ids.Add(reader.GetInt32(0));
			count++;
		}

		return count;
	}

	private readonly record struct UsedIdQuery(string TableName, string Sql);
}
