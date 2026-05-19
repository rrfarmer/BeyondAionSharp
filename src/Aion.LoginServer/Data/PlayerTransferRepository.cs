using Aion.Commons.Database;
using Aion.LoginServer.Model;
using MySqlConnector;

namespace Aion.LoginServer.Data;

public interface IPlayerTransferRepository
{
	Task<IReadOnlyCollection<PlayerTransferTask>> GetNewAsync(CancellationToken cancellationToken = default);

	Task<bool> UpdateAsync(PlayerTransferTask task, CancellationToken cancellationToken = default);
}

public sealed class PlayerTransferRepository : IPlayerTransferRepository
{
	public async Task<IReadOnlyCollection<PlayerTransferTask>> GetNewAsync(CancellationToken cancellationToken = default)
	{
		var result = new List<PlayerTransferTask>();
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT * FROM player_transfers WHERE `status` = ?";
		command.Parameters.Add(new MySqlParameter { Value = PlayerTransferTask.StatusWait });
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			result.Add(
				new PlayerTransferTask
				{
					Id = reader.GetInt32("id"),
					SourceServerId = reader.GetByte("source_server"),
					TargetServerId = reader.GetByte("target_server"),
					SourceAccountId = reader.GetInt32("source_account_id"),
					TargetAccountId = reader.GetInt32("target_account_id"),
					PlayerId = reader.GetInt32("player_id"),
				});
		}

		return result;
	}

	public async Task<bool> UpdateAsync(PlayerTransferTask task, CancellationToken cancellationToken = default)
	{
		var timeColumn = task.Status switch
		{
			PlayerTransferTask.StatusActive => ", time_performed=NOW()",
			PlayerTransferTask.StatusDone or PlayerTransferTask.StatusError => ", time_done=NOW()",
			_ => string.Empty
		};

		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = $"UPDATE player_transfers SET status=?, comment=?{timeColumn} WHERE id=?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = task.Status },
				new MySqlParameter { Value = (object?)task.Comment ?? DBNull.Value },
				new MySqlParameter { Value = task.Id },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}
}
