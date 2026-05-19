using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IAccountRepository
{
	Task<Account?> GetAccountByNameAsync(string name, bool useExternalAuth, CancellationToken cancellationToken = default);

	Task<Account?> GetAccountByIdAsync(int id, bool useExternalAuth, CancellationToken cancellationToken = default);

	Task<bool> InsertAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default);

	Task UpdateLastIpAsync(int accountId, string ip, CancellationToken cancellationToken = default);

	Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default);

	Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default);

	Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default);

	Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default);

	Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default);
}

public sealed class AccountRepository : IAccountRepository
{
	private readonly IAccountTimeRepository _accountTimeRepository;

	public AccountRepository(IAccountTimeRepository accountTimeRepository)
	{
		_accountTimeRepository = accountTimeRepository;
	}

	public Task<Account?> GetAccountByNameAsync(string name, bool useExternalAuth, CancellationToken cancellationToken = default)
	{
		var column = useExternalAuth ? "ext_auth_name" : "name";
		return GetAccountAsync($"SELECT * FROM account_data WHERE `{column}` = ?", name, useExternalAuth, cancellationToken);
	}

	public Task<Account?> GetAccountByIdAsync(int id, bool useExternalAuth, CancellationToken cancellationToken = default)
	{
		return GetAccountAsync("SELECT * FROM account_data WHERE `id` = ?", id, useExternalAuth, cancellationToken);
	}

	public async Task<bool> InsertAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default)
	{
		var nameColumn = useExternalAuth ? "ext_auth_name" : "name";
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = $"""
			INSERT INTO account_data(`{nameColumn}`, `password`, access_level, membership, activated, last_server, last_ip, last_mac, ip_force, toll)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = account.Name },
				new MySqlParameter { Value = account.PasswordHash },
				new MySqlParameter { Value = account.AccessLevel },
				new MySqlParameter { Value = account.Membership },
				new MySqlParameter { Value = account.Activated },
				new MySqlParameter { Value = account.LastServer },
				new MySqlParameter { Value = (object?)account.LastIp ?? DBNull.Value },
				new MySqlParameter { Value = account.LastMac },
				new MySqlParameter { Value = (object?)account.IpForce ?? DBNull.Value },
				new MySqlParameter { Value = account.Toll },
			});

		var rows = await command.ExecuteNonQueryAsync(cancellationToken);
		if (rows == 0)
			return false;

		account.Id = (int)command.LastInsertedId;
		account.CreationDate = DateTime.UtcNow;
		account.AccountTime = new AccountTime();
		await _accountTimeRepository.UpdateAccountTimeAsync(account.Id, account.AccountTime, cancellationToken);
		return true;
	}

	public async Task UpdateLastIpAsync(int accountId, string ip, CancellationToken cancellationToken = default)
	{
		await ExecuteAsync("UPDATE account_data SET last_ip = ? WHERE id = ?", cancellationToken, ip, accountId);
	}

	public Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default)
	{
		return ExecuteWithResultAsync("UPDATE account_data SET last_mac = ? WHERE id = ?", cancellationToken, mac, accountId);
	}

	public Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default)
	{
		return ExecuteWithResultAsync("UPDATE account_data SET last_hdd_serial = ? WHERE id = ?", cancellationToken, hddSerial, accountId);
	}

	public Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default)
	{
		return ExecuteWithResultAsync("UPDATE account_data SET allowed_hdd_serial = ? WHERE id = ?", cancellationToken, hddSerial, accountId);
	}

	public async Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default)
	{
		await ExecuteAsync("UPDATE account_data SET last_server = ? WHERE id = ?", cancellationToken, lastServer, accountId);
	}

	public async Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default)
	{
		await ExecuteAsync("UPDATE account_data SET membership = old_membership, expire = NULL WHERE id = ? and expire < CURRENT_TIMESTAMP", cancellationToken, accountId);
	}

	private async Task<Account?> GetAccountAsync(string sql, object parameter, bool useExternalAuth, CancellationToken cancellationToken)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.Add(new MySqlParameter { Value = parameter });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return null;

		var account = new Account
		{
			Id = reader.GetInt32("id"),
			Name = reader.GetString(useExternalAuth ? "ext_auth_name" : "name"),
			PasswordHash = reader.GetString("password"),
			CreationDate = reader.GetDateTime("creation_date"),
			AccessLevel = reader.GetByte("access_level"),
			Membership = reader.GetByte("membership"),
			Activated = reader.GetByte("activated"),
			LastServer = reader.GetSByte("last_server"),
			LastIp = reader.IsDBNull(reader.GetOrdinal("last_ip")) ? null : reader.GetString("last_ip"),
			LastMac = reader.GetString("last_mac"),
			IpForce = reader.IsDBNull(reader.GetOrdinal("ip_force")) ? null : reader.GetString("ip_force"),
			AllowedHddSerial = reader.IsDBNull(reader.GetOrdinal("allowed_hdd_serial")) ? null : reader.GetString("allowed_hdd_serial"),
			Toll = reader.GetInt64("toll"),
		};
		account.AccountTime = await _accountTimeRepository.GetAccountTimeAsync(account.Id, cancellationToken)
			?? throw new InvalidOperationException($"Account time for account {account.Id} is null.");
		return account;
	}

	private static async Task ExecuteAsync(string sql, CancellationToken cancellationToken, params object?[] parameters)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		foreach (var parameter in parameters)
			command.Parameters.Add(new MySqlParameter { Value = parameter ?? DBNull.Value });
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<bool> ExecuteWithResultAsync(string sql, CancellationToken cancellationToken, params object?[] parameters)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		foreach (var parameter in parameters)
			command.Parameters.Add(new MySqlParameter { Value = parameter ?? DBNull.Value });
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}
}
