using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IBannedMacRepository
{
	Task<IReadOnlyDictionary<string, BannedMacEntry>> LoadAsync(CancellationToken cancellationToken = default);

	Task<bool> UpdateAsync(BannedMacEntry entry, CancellationToken cancellationToken = default);

	Task<bool> RemoveAsync(string address, CancellationToken cancellationToken = default);

	Task CleanExpiredBansAsync(CancellationToken cancellationToken = default);
}

public sealed class BannedMacRepository : IBannedMacRepository
{
	public async Task<IReadOnlyDictionary<string, BannedMacEntry>> LoadAsync(CancellationToken cancellationToken = default)
	{
		var result = new Dictionary<string, BannedMacEntry>();
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT `address`,CAST(FLOOR(UNIX_TIMESTAMP(`time`) * 1000) AS SIGNED) AS `time_epoch_millis`,`details` FROM `banned_mac`";
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var address = reader.GetString("address");
			result[address] = new BannedMacEntry(
				address,
				DatabaseTimestamp.FromUnixTimeMilliseconds(reader.GetInt64("time_epoch_millis")),
				reader.GetString("details"));
		}

		return result;
	}

	public async Task<bool> UpdateAsync(BannedMacEntry entry, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "REPLACE INTO `banned_mac` (`address`,`time`,`details`) VALUES (?,FROM_UNIXTIME(? / 1000.0),?)";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = entry.Mac },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(entry.Time) },
				new MySqlParameter { Value = entry.Details },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<bool> RemoveAsync(string address, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM `banned_mac` WHERE address=?";
		command.Parameters.Add(new MySqlParameter { Value = address });
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM `banned_mac` WHERE time < current_date";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
