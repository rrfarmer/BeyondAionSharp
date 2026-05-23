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
		return await HandleDeathAsync(
			npc,
			looter,
			groupMembers,
			freeForAllDelay,
			decayDelay,
			options: null,
			cancellationToken);
	}

	public async ValueTask<WorldNpcDeathDropWorkflowResult> HandleDeathAsync(
		IWorldNpcObject? npc,
		Player? looter,
		IReadOnlyList<Player>? groupMembers = null,
		TimeSpan? freeForAllDelay = null,
		TimeSpan? decayDelay = null,
		WorldNpcDeathDropOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: controllers/NpcController.onDie asks ALLOW_RESPAWN, REWARD_LOOT, and ALLOW_DECAY around reward/drop registration.
		if (npc == null)
			return WorldNpcDeathDropWorkflowResult.MissingNpc();

		var deathOptions = options ?? WorldNpcDeathDropOptions.Default;
		var respawnScheduled = deathOptions.AllowRespawn && _spawnService.TryScheduleRespawn(npc.ObjectId);
		var dropRegistration = deathOptions.RewardLoot
			? await _dropRegistrationWorkflow.RegisterCustomDropsAsync(
				npc,
				looter,
				groupMembers,
				freeForAllDelay: freeForAllDelay,
				cancellationToken: cancellationToken)
			: WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.LootRewardDisabled);
		var decayScheduled = false;
		var staticPlaceableDespawned = false;
		var deletedImmediately = false;
		if (deathOptions.AllowDecay)
		{
			decayScheduled = _spawnService.TryScheduleWorldNpcDecayTask(npc.ObjectId, decayDelay);
			staticPlaceableDespawned = _spawnService.TryDespawnStaticPlaceableForWorldNpc(npc.ObjectId);
		}
		else
		{
			// Java parity: controllers/NpcController.onDie deletes immediately when ALLOW_DECAY is false.
			deletedImmediately = _spawnService.TryDespawnWorldNpc(npc.ObjectId);
		}

		return new WorldNpcDeathDropWorkflowResult(
			WorldNpcDeathDropWorkflowStatus.Scheduled,
			dropRegistration,
			respawnScheduled,
			decayScheduled,
			staticPlaceableDespawned,
			deletedImmediately);
	}
}

public sealed record WorldNpcDeathDropOptions(
	bool AllowRespawn,
	bool RewardLoot,
	bool AllowDecay)
{
	public static WorldNpcDeathDropOptions Default { get; } = new(
		AllowRespawn: true,
		RewardLoot: true,
		AllowDecay: true);
}

public sealed record WorldNpcDeathDropWorkflowResult(
	WorldNpcDeathDropWorkflowStatus Status,
	WorldNpcDropRegistrationWorkflowResult DropRegistration,
	bool RespawnScheduled,
	bool DecayScheduled,
	bool StaticPlaceableDespawned,
	bool DeletedImmediately = false)
{
	public static WorldNpcDeathDropWorkflowResult MissingNpc()
	{
		return new WorldNpcDeathDropWorkflowResult(
			WorldNpcDeathDropWorkflowStatus.MissingNpc,
			WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.MissingNpc),
			RespawnScheduled: false,
			DecayScheduled: false,
			StaticPlaceableDespawned: false,
			DeletedImmediately: false);
	}
}

public enum WorldNpcDeathDropWorkflowStatus
{
	MissingNpc,
	Scheduled,
}
