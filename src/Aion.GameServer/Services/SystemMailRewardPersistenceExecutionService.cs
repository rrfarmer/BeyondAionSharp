namespace Aion.GameServer.Services;

public sealed class SystemMailRewardPersistenceExecutionService
{
	public async Task<SystemMailRewardPersistenceExecutionResult> ExecuteAsync(
		SystemMailRewardPersistencePlan plan,
		ISystemMailRewardPersistenceOperationExecutor executor,
		SystemMailRewardPersistenceExecutionOptions options,
		CancellationToken cancellationToken = default)
	{
		// Java parity: SystemMailService.sendMail returns false when MailDAO.storeLetter or InventoryDAO.store fails.
		if (!options.EnableLivePersistence)
			return SystemMailRewardPersistenceExecutionResult.Disabled(plan);

		if (!plan.Applied)
			return SystemMailRewardPersistenceExecutionResult.Skipped(plan);

		var executedOperations = new List<SystemMailRewardPersistenceOperation>();
		var failedOperations = new List<SystemMailRewardPersistenceOperation>();

		foreach (var operation in plan.Operations)
		{
			var succeeded = await executor.ExecuteAsync(operation, cancellationToken);
			executedOperations.Add(operation);

			if (succeeded)
				continue;

			failedOperations.Add(operation);
			if (operation.StopsOnFailure)
				return SystemMailRewardPersistenceExecutionResult.Failed(plan, executedOperations, failedOperations, ToFailureStatus(operation.Kind));
		}

		return failedOperations.Count == 0
			? SystemMailRewardPersistenceExecutionResult.Completed(plan, executedOperations)
			: SystemMailRewardPersistenceExecutionResult.CompletedWithNonCriticalFailures(plan, executedOperations, failedOperations);
	}

	private static SystemMailRewardPersistenceExecutionStatus ToFailureStatus(SystemMailRewardPersistenceOperationKind kind)
	{
		return kind switch
		{
			SystemMailRewardPersistenceOperationKind.StoreLetter => SystemMailRewardPersistenceExecutionStatus.StoreLetterFailed,
			SystemMailRewardPersistenceOperationKind.StoreAttachedItem => SystemMailRewardPersistenceExecutionStatus.StoreAttachedItemFailed,
			_ => SystemMailRewardPersistenceExecutionStatus.CriticalOperationFailed,
		};
	}
}

public interface ISystemMailRewardPersistenceOperationExecutor
{
	Task<bool> ExecuteAsync(SystemMailRewardPersistenceOperation operation, CancellationToken cancellationToken = default);
}

public sealed record SystemMailRewardPersistenceExecutionOptions(bool EnableLivePersistence)
{
	public static SystemMailRewardPersistenceExecutionOptions Disabled { get; } = new(false);
}

public sealed record SystemMailRewardPersistenceExecutionResult(
	SystemMailRewardPersistenceExecutionStatus Status,
	SystemMailRewardPersistencePlan Plan,
	IReadOnlyList<SystemMailRewardPersistenceOperation> ExecutedOperations,
	IReadOnlyList<SystemMailRewardPersistenceOperation> FailedOperations,
	bool IsLive,
	string JavaSource)
{
	public bool Applied => Status is SystemMailRewardPersistenceExecutionStatus.Completed
		or SystemMailRewardPersistenceExecutionStatus.CompletedWithNonCriticalFailures;

	public static SystemMailRewardPersistenceExecutionResult Disabled(SystemMailRewardPersistencePlan plan)
	{
		return new SystemMailRewardPersistenceExecutionResult(
			SystemMailRewardPersistenceExecutionStatus.Disabled,
			plan,
			Array.Empty<SystemMailRewardPersistenceOperation>(),
			Array.Empty<SystemMailRewardPersistenceOperation>(),
			IsLive: false,
			JavaSource: "SystemMailService.sendMail execution disabled by C# opt-in gate");
	}

	public static SystemMailRewardPersistenceExecutionResult Skipped(SystemMailRewardPersistencePlan plan)
	{
		return new SystemMailRewardPersistenceExecutionResult(
			SystemMailRewardPersistenceExecutionStatus.SkippedPlanNotApplied,
			plan,
			Array.Empty<SystemMailRewardPersistenceOperation>(),
			Array.Empty<SystemMailRewardPersistenceOperation>(),
			IsLive: false,
			JavaSource: "SystemMailService.sendMail skipped before persistence execution");
	}

	public static SystemMailRewardPersistenceExecutionResult Completed(
		SystemMailRewardPersistencePlan plan,
		IReadOnlyList<SystemMailRewardPersistenceOperation> executedOperations)
	{
		return new SystemMailRewardPersistenceExecutionResult(
			SystemMailRewardPersistenceExecutionStatus.Completed,
			plan,
			executedOperations,
			Array.Empty<SystemMailRewardPersistenceOperation>(),
			IsLive: true,
			JavaSource: plan.JavaSource);
	}

	public static SystemMailRewardPersistenceExecutionResult CompletedWithNonCriticalFailures(
		SystemMailRewardPersistencePlan plan,
		IReadOnlyList<SystemMailRewardPersistenceOperation> executedOperations,
		IReadOnlyList<SystemMailRewardPersistenceOperation> failedOperations)
	{
		return new SystemMailRewardPersistenceExecutionResult(
			SystemMailRewardPersistenceExecutionStatus.CompletedWithNonCriticalFailures,
			plan,
			executedOperations,
			failedOperations,
			IsLive: true,
			JavaSource: plan.JavaSource);
	}

	public static SystemMailRewardPersistenceExecutionResult Failed(
		SystemMailRewardPersistencePlan plan,
		IReadOnlyList<SystemMailRewardPersistenceOperation> executedOperations,
		IReadOnlyList<SystemMailRewardPersistenceOperation> failedOperations,
		SystemMailRewardPersistenceExecutionStatus status)
	{
		return new SystemMailRewardPersistenceExecutionResult(
			status,
			plan,
			executedOperations,
			failedOperations,
			IsLive: true,
			JavaSource: plan.JavaSource);
	}
}

public enum SystemMailRewardPersistenceExecutionStatus
{
	Disabled,
	SkippedPlanNotApplied,
	Completed,
	CompletedWithNonCriticalFailures,
	StoreLetterFailed,
	StoreAttachedItemFailed,
	CriticalOperationFailed,
}
