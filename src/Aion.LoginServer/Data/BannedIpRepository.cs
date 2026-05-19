using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IBannedIpRepository
{
	Task CleanExpiredBansAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyCollection<BannedIp>> GetAllBansAsync(CancellationToken cancellationToken = default);
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
		command.CommandText = "SELECT * FROM banned_ip";
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			result.Add(new BannedIp
			{
				Id = reader.GetInt32("id"),
				Mask = reader.GetString("mask"),
				TimeEnd = reader.IsDBNull(reader.GetOrdinal("time_end")) ? null : reader.GetDateTime("time_end"),
			});
		}
		return result;
	}
}
