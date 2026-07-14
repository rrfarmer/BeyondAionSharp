using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;
using System.Data.Common;

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
		command.CommandText = $"""
			SELECT *,
				{DatabaseTimestamp.UnixTimeMillisecondsSql("last_active", "last_active_epoch_millis")},
				{DatabaseTimestamp.UnixTimeMillisecondsSql("penalty_end", "penalty_end_epoch_millis")},
				{DatabaseTimestamp.UnixTimeMillisecondsSql("expiration_time", "expiration_time_epoch_millis")}
			FROM account_time WHERE account_id = ?
			""";
		command.Parameters.Add(new MySqlParameter { Value = accountId });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return new AccountTime();

		return ReadAccountTime(reader);
	}

	internal static AccountTime ReadAccountTime(DbDataReader reader)
	{
		return new AccountTime
		{
			LastLoginTime = DatabaseTimestamp.ReadUtcDateTime(reader, "last_active_epoch_millis"),
			SessionDuration = GetJavaInt64(reader, "session_duration"),
			AccumulatedOnlineTime = GetJavaInt64(reader, "accumulated_online"),
			AccumulatedRestTime = GetJavaInt64(reader, "accumulated_rest"),
			PenaltyEnd = DatabaseTimestamp.ReadNullableUtcDateTime(reader, "penalty_end_epoch_millis"),
			ExpirationTime = DatabaseTimestamp.ReadNullableUtcDateTime(reader, "expiration_time_epoch_millis"),
		};
	}

	private static long GetJavaInt64(DbDataReader reader, string columnName)
	{
		int ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? 0L : reader.GetInt64(ordinal);
	}

	public async Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = """
			REPLACE INTO account_time (account_id, last_active, expiration_time, session_duration, accumulated_online, accumulated_rest, penalty_end)
			VALUES (?, FROM_UNIXTIME(? / 1000.0), FROM_UNIXTIME(? / 1000.0), ?, ?, ?, FROM_UNIXTIME(? / 1000.0))
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = accountId },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(accountTime.LastLoginTime) },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(accountTime.ExpirationTime) },
				new MySqlParameter { Value = accountTime.SessionDuration },
				new MySqlParameter { Value = accountTime.AccumulatedOnlineTime },
				new MySqlParameter { Value = accountTime.AccumulatedRestTime },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(accountTime.PenaltyEnd) },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
