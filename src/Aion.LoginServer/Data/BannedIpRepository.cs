using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;
using System.Data.Common;

namespace Aion.LoginServer.Data;

public interface IBannedIpRepository
{
	Task CleanExpiredBansAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyCollection<BannedIp>> GetAllBansAsync(CancellationToken cancellationToken = default);

	Task<bool> InsertAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default);

	Task<bool> RemoveAsync(string mask, CancellationToken cancellationToken = default);
}

public sealed class BannedIpRepository : IBannedIpRepository
{
	public async Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM banned_ip WHERE time_end < current_timestamp AND time_end IS NOT NULL";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyCollection<BannedIp>> GetAllBansAsync(CancellationToken cancellationToken = default)
	{
		var result = new List<BannedIp>();
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = $"SELECT *, {DatabaseTimestamp.UnixTimeMillisecondsSql("time_end", "time_end_epoch_millis")} FROM banned_ip";
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			result.Add(ReadBannedIp(reader));
		}
		return result;
	}

	public async Task<bool> InsertAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "INSERT INTO banned_ip(mask, time_end) VALUES (?, FROM_UNIXTIME(? / 1000.0))";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = mask },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(expireTime) },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<bool> RemoveAsync(string mask, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM banned_ip WHERE mask = ?";
		command.Parameters.Add(new MySqlParameter { Value = mask });
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	internal static BannedIp ReadBannedIp(DbDataReader reader)
	{
		return new BannedIp
		{
			Id = reader.GetInt32(reader.GetOrdinal("id")),
			Mask = reader.GetString(reader.GetOrdinal("mask")),
			TimeEnd = DatabaseTimestamp.ReadNullableUtcDateTime(reader, "time_end_epoch_millis"),
		};
	}
}
