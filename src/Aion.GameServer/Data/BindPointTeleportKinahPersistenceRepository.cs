using Aion.Commons.Database;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IBindPointTeleportKinahPersistenceRepository
{
	Task<int> ExecuteKinahCountUpdateAsync(
		BindPointTeleportKinahPersistenceOperationPlan operationPlan,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyBindPointTeleportKinahPersistenceRepository : IBindPointTeleportKinahPersistenceRepository
{
	public Task<int> ExecuteKinahCountUpdateAsync(
		BindPointTeleportKinahPersistenceOperationPlan operationPlan,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(0);
	}
}

public sealed class MySqlBindPointTeleportKinahPersistenceRepository : IBindPointTeleportKinahPersistenceRepository
{
	private readonly ILogger<MySqlBindPointTeleportKinahPersistenceRepository> _logger;

	public MySqlBindPointTeleportKinahPersistenceRepository(
		ILogger<MySqlBindPointTeleportKinahPersistenceRepository> logger)
	{
		_logger = logger;
	}

	public async Task<int> ExecuteKinahCountUpdateAsync(
		BindPointTeleportKinahPersistenceOperationPlan operationPlan,
		CancellationToken cancellationToken = default)
	{
		// Java parity: InventoryDAO.updateItems writes dirty inventory rows later. This adapter
		// executes the narrower C# owner-checked Kinah count update only when an opt-in caller enables it.
		if (!operationPlan.ShouldExecuteSql || string.IsNullOrWhiteSpace(operationPlan.Sql))
			return 0;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = operationPlan.Sql;
			foreach (var parameter in operationPlan.Parameters)
				command.Parameters.Add(new MySqlParameter { Value = parameter.Value });
			return await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not persist scheduled bind-point Kinah item {KinahObjectId} for player {PlayerObjectId}",
				operationPlan.KinahObjectId,
				operationPlan.PlayerObjectId);
			throw;
		}
	}
}
