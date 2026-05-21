using Aion.Commons.Database;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IMotionRepository
{
	Task<bool> UpdateMotionActiveAsync(
		int playerObjectId,
		int motionId,
		bool isActive,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyMotionRepository : IMotionRepository
{
	public Task<bool> UpdateMotionActiveAsync(
		int playerObjectId,
		int motionId,
		bool isActive,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlMotionRepository : IMotionRepository
{
	private readonly ILogger<MySqlMotionRepository> _logger;

	public MySqlMotionRepository(ILogger<MySqlMotionRepository> logger)
	{
		_logger = logger;
	}

	public async Task<bool> UpdateMotionActiveAsync(
		int playerObjectId,
		int motionId,
		bool isActive,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MotionDAO.updateMotion.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE player_motions SET active = ? WHERE player_id = ? AND motion_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = isActive },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = motionId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not update motion {MotionId} for player {PlayerObjectId}", motionId, playerObjectId);
			return false;
		}
	}
}
