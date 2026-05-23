using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDeathDropWorkflowService
{
	private readonly WorldNpcSpawnService _spawnService;
	private readonly WorldNpcDropRegistrationWorkflowService _dropRegistrationWorkflow;

	public WorldNpcDeathDropWorkflowService(
		WorldNpcSpawnService spawnService,
		WorldNpcDropRegistrationWorkflowService dropRegistrationWorkflow)
	{
		_spawnService = spawnService;
		_dropRegistrationWorkflow = dropRegistrationWorkflow;
	}

	public async ValueTask<WorldNpcDeathDropWorkflowResult> HandleCustomDropDeathAsync(
		IWorldNpcObject? npc,
		Player? looter,
		IReadOnlyList<Player>? groupMembers = null,
		TimeSpan? freeForAllDelay = null,
		TimeSpan? decayDelay = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: controllers/NpcController.onDie schedules respawn before reward/drop events, then schedules drop-aware corpse decay.
		if (npc == null)
			return WorldNpcDeathDropWorkflowResult.MissingNpc();

		var respawnScheduled = _spawnService.TryScheduleRespawn(npc.ObjectId);
		var dropRegistration = await _dropRegistrationWorkflow.RegisterCustomDropsAsync(
			npc,
			looter,
			groupMembers,
			freeForAllDelay: freeForAllDelay,
			cancellationToken: cancellationToken);
		var decayScheduled = _spawnService.TryScheduleWorldNpcDecayTask(npc.ObjectId, decayDelay);
		var staticPlaceableDespawned = _spawnService.TryDespawnStaticPlaceableForWorldNpc(npc.ObjectId);

		return new WorldNpcDeathDropWorkflowResult(
			WorldNpcDeathDropWorkflowStatus.Scheduled,
			dropRegistration,
			respawnScheduled,
			decayScheduled,
			staticPlaceableDespawned);
	}
}

public sealed record WorldNpcDeathDropWorkflowResult(
	WorldNpcDeathDropWorkflowStatus Status,
	WorldNpcDropRegistrationWorkflowResult DropRegistration,
	bool RespawnScheduled,
	bool DecayScheduled,
	bool StaticPlaceableDespawned)
{
	public static WorldNpcDeathDropWorkflowResult MissingNpc()
	{
		return new WorldNpcDeathDropWorkflowResult(
			WorldNpcDeathDropWorkflowStatus.MissingNpc,
			WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.MissingNpc),
			RespawnScheduled: false,
			DecayScheduled: false,
			StaticPlaceableDespawned: false);
	}
}

public enum WorldNpcDeathDropWorkflowStatus
{
	MissingNpc,
	Scheduled,
}
