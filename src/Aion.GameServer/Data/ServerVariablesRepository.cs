using System.Globalization;
using Aion.Commons.Database;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IServerVariablesRepository
{
	Task<int?> LoadIntAsync(string key, CancellationToken cancellationToken = default);

	Task<long?> LoadLongAsync(string key, CancellationToken cancellationToken = default);

	Task<bool> StoreAsync(string key, object value, CancellationToken cancellationToken = default);
}

public sealed class EmptyServerVariablesRepository : IServerVariablesRepository
{
	public Task<int?> LoadIntAsync(string key, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<int?>(null);
	}

	public Task<long?> LoadLongAsync(string key, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<long?>(null);
	}

	public Task<bool> StoreAsync(string key, object value, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}
}

public sealed class MySqlServerVariablesRepository : IServerVariablesRepository
{
	private readonly ILogger<MySqlServerVariablesRepository> _logger;

	public MySqlServerVariablesRepository(ILogger<MySqlServerVariablesRepository> logger)
	{
		_logger = logger;
	}

	public async Task<int?> LoadIntAsync(string key, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/ServerVariablesDAO.loadInt.
		var value = await LoadAsync(key, cancellationToken);
		return value == null ? null : int.Parse(value, CultureInfo.InvariantCulture);
	}

	public async Task<long?> LoadLongAsync(string key, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/ServerVariablesDAO.loadLong.
		var value = await LoadAsync(key, cancellationToken);
		return value == null ? null : long.Parse(value, CultureInfo.InvariantCulture);
	}

	public async Task<bool> StoreAsync(string key, object value, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/ServerVariablesDAO.store.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "REPLACE INTO server_variables (`key`, `value`) VALUES (?, ?)";
			command.Parameters.Add(new MySqlParameter { Value = key });
			command.Parameters.Add(new MySqlParameter { Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty });
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error storing {Value} for variable {Key}", value, key);
			return false;
		}
	}

	private async Task<string?> LoadAsync(string key, CancellationToken cancellationToken)
	{
		// Java parity: dao/ServerVariablesDAO.load.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT `value` FROM server_variables WHERE `key` = ?";
			command.Parameters.Add(new MySqlParameter { Value = key });
			var value = await command.ExecuteScalarAsync(cancellationToken);
			return value?.ToString();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error loading value for {Key}", key);
			return null;
		}
	}
}
