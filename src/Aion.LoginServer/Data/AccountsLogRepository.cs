using Aion.Commons.Database;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IAccountsLogRepository
{
	Task AddRecordAsync(int accountId, byte gameServerId, DateTime time, string ip, string mac, string hddSerial, CancellationToken cancellationToken = default);
}

public sealed class AccountsLogRepository : IAccountsLogRepository
{
	public async Task AddRecordAsync(int accountId, byte gameServerId, DateTime time, string ip, string mac, string hddSerial, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "INSERT INTO account_login_history(account_id, gameserver_id, date, ip, mac, hdd_serial) VALUES (?, ?, ?, ?, ?, ?)";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = accountId },
				new MySqlParameter { Value = gameServerId },
				new MySqlParameter { Value = time },
				new MySqlParameter { Value = ip },
				new MySqlParameter { Value = mac },
				new MySqlParameter { Value = hddSerial },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
