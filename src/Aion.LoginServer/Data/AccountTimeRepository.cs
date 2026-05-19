using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IAccountTimeRepository
{
	Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default);

	Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default);
}

public sealed class AccountTimeRepository : IAccountTimeRepository
{
	public async Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT * FROM account_time WHERE account_id = ?";
		command.Parameters.Add(new MySqlParameter { Value = accountId });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return new AccountTime();

		return new AccountTime
		{
			LastLoginTime = reader.GetDateTime("last_active"),
			SessionDuration = reader.GetInt64("session_duration"),
			AccumulatedOnlineTime = reader.GetInt64("accumulated_online"),
			AccumulatedRestTime = reader.GetInt64("accumulated_rest"),
			PenaltyEnd = reader.IsDBNull(reader.GetOrdinal("penalty_end")) ? null : reader.GetDateTime("penalty_end"),
			ExpirationTime = reader.IsDBNull(reader.GetOrdinal("expiration_time")) ? null : reader.GetDateTime("expiration_time"),
		};
	}

	public async Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = """
			REPLACE INTO account_time (account_id, last_active, expiration_time, session_duration, accumulated_online, accumulated_rest, penalty_end)
			VALUES (?, ?, ?, ?, ?, ?, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = accountId },
				new MySqlParameter { Value = accountTime.LastLoginTime },
				new MySqlParameter { Value = (object?)accountTime.ExpirationTime ?? DBNull.Value },
				new MySqlParameter { Value = accountTime.SessionDuration },
				new MySqlParameter { Value = accountTime.AccumulatedOnlineTime },
				new MySqlParameter { Value = accountTime.AccumulatedRestTime },
				new MySqlParameter { Value = (object?)accountTime.PenaltyEnd ?? DBNull.Value },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
