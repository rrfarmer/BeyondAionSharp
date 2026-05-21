using Aion.Commons.Database;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface ISocialRepository
{
	Task<bool> DeleteFriendsAsync(int playerObjectId, int friendObjectId, CancellationToken cancellationToken = default);

	Task<bool> SetFriendMemoAsync(int playerObjectId, int friendObjectId, string memo, CancellationToken cancellationToken = default);

	Task<bool> DeleteBlockedUserAsync(int playerObjectId, int blockedPlayerObjectId, CancellationToken cancellationToken = default);

	Task<bool> SetBlockedReasonAsync(int playerObjectId, int blockedPlayerObjectId, string reason, CancellationToken cancellationToken = default);
}

public sealed class EmptySocialRepository : ISocialRepository
{
	public Task<bool> DeleteFriendsAsync(int playerObjectId, int friendObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> SetFriendMemoAsync(int playerObjectId, int friendObjectId, string memo, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> DeleteBlockedUserAsync(int playerObjectId, int blockedPlayerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> SetBlockedReasonAsync(int playerObjectId, int blockedPlayerObjectId, string reason, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlSocialRepository : ISocialRepository
{
	private readonly ILogger<MySqlSocialRepository> _logger;

	public MySqlSocialRepository(ILogger<MySqlSocialRepository> logger)
	{
		_logger = logger;
	}

	public async Task<bool> DeleteFriendsAsync(
		int playerObjectId,
		int friendObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/FriendListDAO.delFriends deletes both directions in one batch.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				DELETE FROM friends
				WHERE (player = ? AND friend = ?) OR (player = ? AND friend = ?)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = friendObjectId },
					new MySqlParameter { Value = friendObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not delete friendship between player {PlayerObjectId} and friend {FriendObjectId}",
				playerObjectId,
				friendObjectId);
			return false;
		}
	}

	public async Task<bool> SetFriendMemoAsync(
		int playerObjectId,
		int friendObjectId,
		string memo,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/FriendListDAO.setFriendMemo.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE friends SET memo = ? WHERE player = ? AND friend = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = memo },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = friendObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not update friend memo for player {PlayerObjectId} and friend {FriendObjectId}",
				playerObjectId,
				friendObjectId);
			return false;
		}
	}

	public async Task<bool> DeleteBlockedUserAsync(
		int playerObjectId,
		int blockedPlayerObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/BlockListDAO.delBlockedUser.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM blocks WHERE player = ? AND blocked_player = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = blockedPlayerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not delete blocked user {BlockedPlayerObjectId} for player {PlayerObjectId}",
				blockedPlayerObjectId,
				playerObjectId);
			return false;
		}
	}

	public async Task<bool> SetBlockedReasonAsync(
		int playerObjectId,
		int blockedPlayerObjectId,
		string reason,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/BlockListDAO.setReason.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE blocks SET reason = ? WHERE player = ? AND blocked_player = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = reason },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = blockedPlayerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not update block reason for player {PlayerObjectId} and blocked user {BlockedPlayerObjectId}",
				playerObjectId,
				blockedPlayerObjectId);
			return false;
		}
	}
}
