using Aion.Commons.Database;

namespace Aion.LoginServer.Data;

public interface IPremiumRepository
{
	Task<long> GetPointsAsync(int accountId, CancellationToken cancellationToken = default);

	Task<bool> UpdatePointsAsync(int accountId, long points, long required, CancellationToken cancellationToken = default);
}

public sealed class PremiumRepository : IPremiumRepository
{
	public async Task<long> GetPointsAsync(int accountId, CancellationToken cancellationToken = default)
	{
		long points = 0;
		var rewarded = new List<int>();

		await using (var connection = DatabaseFactory.GetConnection())
		{
			await connection.OpenAsync(cancellationToken);
			await using (var command = connection.CreateCommand())
			{
				command.CommandText = "SELECT toll FROM account_data WHERE id=?";
				command.Parameters.Add(new MySqlConnector.MySqlParameter { Value = accountId });
				var result = await command.ExecuteScalarAsync(cancellationToken);
				if (result != null && result != DBNull.Value)
					points = Convert.ToInt64(result);
			}

			await using (var command = connection.CreateCommand())
			{
				command.CommandText = "SELECT uniqId,points FROM account_rewards WHERE accountId=? AND rewarded=0";
				command.Parameters.Add(new MySqlConnector.MySqlParameter { Value = accountId });
				await using var reader = await command.ExecuteReaderAsync(cancellationToken);
				if (await reader.ReadAsync(cancellationToken))
				{
					var uniqId = reader.GetInt32("uniqId");
					points += reader.GetInt64("points");
					rewarded.Add(uniqId);
				}
			}

			if (rewarded.Count > 0)
			{
				await using var command = connection.CreateCommand();
				command.CommandText = "UPDATE account_rewards SET rewarded=1,received=NOW() WHERE uniqId=?";
				var parameter = command.Parameters.Add(new MySqlConnector.MySqlParameter { Value = 0 });
				foreach (var uniqId in rewarded)
				{
					parameter.Value = uniqId;
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}

		return points;
	}

	public async Task<bool> UpdatePointsAsync(int accountId, long points, long required, CancellationToken cancellationToken = default)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "UPDATE account_data SET toll=? WHERE id=?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlConnector.MySqlParameter { Value = points - required },
				new MySqlConnector.MySqlParameter { Value = accountId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}
}
