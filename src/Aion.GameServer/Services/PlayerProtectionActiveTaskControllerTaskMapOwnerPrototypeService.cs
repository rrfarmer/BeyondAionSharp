namespace Aion.GameServer.Services;

public sealed record PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot(
	int OwnerObjectId,
	int TaskCount,
	IReadOnlyList<int> TaskIdOrdinals,
	IReadOnlyList<string> TaskIdNames,
	bool IsControllerOwned,
	bool IsLive,
	string JavaSource
);

public sealed class PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService
{
	public const int ProtectionActiveTaskIdOrdinal = 3;
	public const string ProtectionActiveTaskIdName = "PROTECTION_ACTIVE";

	private readonly PlayerProtectionActiveTaskTaskMapAdapterService _adapter = new();

	public PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeService(int ownerObjectId)
	{
		// Java parity: CreatureController owns the per-controller task map keyed by TaskId ordinal.
		// This prototype gives the staged protection-active task flow an owner-shaped seam without
		// claiming live scheduler parity.
		OwnerObjectId = ownerObjectId;
	}

	public int OwnerObjectId { get; }

	public bool IsControllerOwned => true;

	public bool IsLive => false;

	public string JavaSource => "CreatureController.tasks owner-shaped non-live prototype for TaskId.PROTECTION_ACTIVE";

	public PlayerProtectionActiveTaskTaskMapOperationResult HasTask() => _adapter.HasTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName);

	public PlayerProtectionActiveTaskTaskMapOperationResult HasScheduledTask() =>
		_adapter.HasScheduledTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName);

	public PlayerProtectionActiveTaskTaskMapOperationResult GetAndRemoveTask() =>
		_adapter.GetAndRemoveTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName);

	public PlayerProtectionActiveTaskTaskMapOperationResult CancelTask() =>
		_adapter.CancelTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName);

	public PlayerProtectionActiveTaskTaskMapOperationResult CancelTaskIfPresent(IPlayerProtectionActiveTaskTaskHandle expectedTask) =>
		_adapter.CancelTaskIfPresent(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName, expectedTask);

	public PlayerProtectionActiveTaskTaskMapOperationResult AddTask(IPlayerProtectionActiveTaskTaskHandle task) =>
		_adapter.AddTask(ProtectionActiveTaskIdOrdinal, ProtectionActiveTaskIdName, task);

	public PlayerProtectionActiveTaskTaskMapOperationResult CancelAllTasks() => _adapter.CancelAllTasks();

	public PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot CreateSnapshot()
	{
		var adapterSnapshot = _adapter.CreateSnapshot();
		return new PlayerProtectionActiveTaskControllerTaskMapOwnerPrototypeSnapshot(
			OwnerObjectId,
			adapterSnapshot.Count,
			adapterSnapshot.TaskIdOrdinals,
			adapterSnapshot.TaskIdNames,
			IsControllerOwned,
			IsLive,
			JavaSource
		);
	}
}
