using Aion.Commons.Database;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IBannedHddRepository
{
	Task<IReadOnlyDictionary<string, DateTime>> LoadAsync(CancellationToken cancellationToken = default);

	Task<bool> UpdateAsync(string serial, DateTime time, CancellationToken cancellationToken = default);

	Task<bool> RemoveAsync(string serial, CancellationToken cancellationToken = default);

	Task CleanExpiredBansAsync(CancellationToken cancellationToken = default);
}

public sealed class BannedHddRepository : IBannedHddRepository
{
	public async Task<IReadOnlyDictionary<string, DateTime>> LoadAsync(CancellationToken cancellationToken = default)
	{
		var result = new Dictionary<string, DateTime>();
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT `serial`,CAST(FLOOR(UNIX_TIMESTAMP(`time`) * 1000) AS SIGNED) AS `time_epoch_millis` FROM `banned_hdd`";
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
			result[reader.GetString("serial")] = DatabaseTimestamp.FromUnixTimeMilliseconds(reader.GetInt64("time_epoch_millis"));
		return result;
	}

	public async Task<bool> UpdateAsync(string serial, DateTime time, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "REPLACE INTO `banned_hdd` (`serial`,`time`) VALUES (?,FROM_UNIXTIME(? / 1000.0))";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = serial },
				new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(time) },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<bool> RemoveAsync(string serial, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM `banned_hdd` WHERE serial=?";
		command.Parameters.Add(new MySqlParameter { Value = serial });
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM `banned_hdd` WHERE time < current_date";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
