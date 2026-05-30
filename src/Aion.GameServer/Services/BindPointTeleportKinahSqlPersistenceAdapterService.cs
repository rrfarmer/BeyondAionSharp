using Aion.GameServer.Data;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahSqlPersistenceAdapterStatus
{
	NoSqlRequired,
	Disabled,
	Saved,
	MissingRow,
	Failed,
}

public sealed record BindPointTeleportKinahSqlPersistenceAdapterPlan(
	BindPointTeleportKinahSqlPersistenceAdapterStatus Status,
	BindPointTeleportKinahPersistenceOperationPlan OperationPlan,
	BindPointTeleportKinahPersistenceResult? PersistenceResult,
	bool WouldExecuteSql,
	bool DidExecuteSql,
	string JavaSource,
	bool IsLive
);

public sealed class BindPointTeleportKinahSqlPersistenceAdapterService
{
	private readonly IBindPointTeleportKinahPersistenceRepository? _repository;
	private readonly bool _enabled;

	public BindPointTeleportKinahSqlPersistenceAdapterService(IBindPointTeleportKinahPersistenceRepository? repository = null, bool enabled = false)
	{
		_repository = repository;
		_enabled = enabled;
	}

	public async Task<BindPointTeleportKinahSqlPersistenceAdapterPlan> ExecuteAsync(
		BindPointTeleportKinahPersistenceOperationPlan operationPlan,
		CancellationToken cancellationToken = default
	)
	{
		// Java parity: InventoryDAO.updateItems batches dirty item rows and does
		// not inspect affected-row counts. This C# seam deliberately keeps the future live
		// write owner-checked and disabled by default.
		if (!operationPlan.ShouldExecuteSql)
		{
			return new BindPointTeleportKinahSqlPersistenceAdapterPlan(
				BindPointTeleportKinahSqlPersistenceAdapterStatus.NoSqlRequired,
				operationPlan,
				BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(operationPlan, affectedRows: 0),
				WouldExecuteSql: false,
				DidExecuteSql: false,
				"Scheduled bind-point Kinah persistence adapter found no SQL operation to execute",
				IsLive: false
			);
		}

		if (!_enabled || _repository == null)
		{
			return new BindPointTeleportKinahSqlPersistenceAdapterPlan(
				BindPointTeleportKinahSqlPersistenceAdapterStatus.Disabled,
				operationPlan,
				PersistenceResult: null,
				WouldExecuteSql: true,
				DidExecuteSql: false,
				"Scheduled bind-point Kinah SQL adapter is disabled; no inventory row update was executed",
				IsLive: false
			);
		}

		try
		{
			var affectedRows = await _repository.ExecuteKinahCountUpdateAsync(operationPlan, cancellationToken);
			var result = BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(operationPlan, affectedRows);
			return new BindPointTeleportKinahSqlPersistenceAdapterPlan(
				result.Status switch
				{
					BindPointTeleportKinahPersistenceStatus.Saved => BindPointTeleportKinahSqlPersistenceAdapterStatus.Saved,
					BindPointTeleportKinahPersistenceStatus.MissingRow => BindPointTeleportKinahSqlPersistenceAdapterStatus.MissingRow,
					_ => BindPointTeleportKinahSqlPersistenceAdapterStatus.Failed,
				},
				operationPlan,
				result,
				WouldExecuteSql: true,
				DidExecuteSql: true,
				result.JavaSource,
				IsLive: true
			);
		}
		catch (Exception ex)
		{
			var result = BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(operationPlan, affectedRows: 0, ex);
			return new BindPointTeleportKinahSqlPersistenceAdapterPlan(
				BindPointTeleportKinahSqlPersistenceAdapterStatus.Failed,
				operationPlan,
				result,
				WouldExecuteSql: true,
				DidExecuteSql: true,
				result.JavaSource,
				IsLive: true
			);
		}
	}
}
