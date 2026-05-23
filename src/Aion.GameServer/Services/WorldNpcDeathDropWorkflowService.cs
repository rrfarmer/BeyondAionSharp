using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDeathDropWorkflowService
{
	private readonly WorldNpcSpawnService _spawnService;
	private readonly WorldNpcDropRegistrationWorkflowService _dropRegistrationWorkflow;
	private readonly WorldNpcAiStateService? _npcAiStates;
	private readonly Func<IWorldNpcObject, CancellationToken, ValueTask<PlayerKiskDespawnResult?>>? _kiskDeathCleanup;
	private readonly Func<PlayerKiskDespawnResult, CancellationToken, ValueTask<PlayerKiskRemovalRuntimeCleanupResult>>? _kiskRemovalCleanup;

	public WorldNpcDeathDropWorkflowService(
		WorldNpcSpawnService spawnService,
		WorldNpcDropRegistrationWorkflowService dropRegistrationWorkflow,
		WorldNpcAiStateService? npcAiStates = null,
		Func<IWorldNpcObject, CancellationToken, ValueTask<PlayerKiskDespawnResult?>>? kiskDeathCleanup = null,
		Func<PlayerKiskDespawnResult, CancellationToken, ValueTask<PlayerKiskRemovalRuntimeCleanupResult>>? kiskRemovalCleanup = null)
	{
		_spawnService = spawnService;
		_dropRegistrationWorkflow = dropRegistrationWorkflow;
		_npcAiStates = npcAiStates;
		_kiskDeathCleanup = kiskDeathCleanup;
		_kiskRemovalCleanup = kiskRemovalCleanup;
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

		var kiskDespawn = _kiskDeathCleanup == null
			? null
			: await _kiskDeathCleanup(npc, cancellationToken);
		if (kiskDespawn?.RemovedRegistry == true)
		{
			var kiskCleanup = _kiskRemovalCleanup == null
				? PlayerKiskRemovalRuntimeCleanupResult.NotApplied
				: await _kiskRemovalCleanup(kiskDespawn, cancellationToken);
			return WorldNpcDeathDropWorkflowResult.KiskRemoved(kiskDespawn, kiskCleanup);
		}

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
		var aiMarkedDied = _npcAiStates?.MarkDied(npc.ObjectId) != null;
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
			deletedImmediately,
			aiMarkedDied);
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
	bool DeletedImmediately = false,
	bool AiMarkedDied = false,
	PlayerKiskDespawnResult? KiskDespawn = null,
	PlayerKiskRemovalRuntimeCleanupResult? KiskRemovalCleanup = null)
{
	public static WorldNpcDeathDropWorkflowResult MissingNpc()
	{
		return new WorldNpcDeathDropWorkflowResult(
			WorldNpcDeathDropWorkflowStatus.MissingNpc,
			WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.MissingNpc),
			RespawnScheduled: false,
			DecayScheduled: false,
			StaticPlaceableDespawned: false,
			DeletedImmediately: false,
			AiMarkedDied: false);
	}

	public static WorldNpcDeathDropWorkflowResult KiskRemoved(
		PlayerKiskDespawnResult kiskDespawn,
		PlayerKiskRemovalRuntimeCleanupResult kiskRemovalCleanup)
	{
		return new WorldNpcDeathDropWorkflowResult(
			WorldNpcDeathDropWorkflowStatus.KiskRemoved,
			WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.KiskRemoved),
			RespawnScheduled: false,
			DecayScheduled: false,
			StaticPlaceableDespawned: false,
			DeletedImmediately: true,
			AiMarkedDied: false,
			kiskDespawn,
			kiskRemovalCleanup);
	}
}

public enum WorldNpcDeathDropWorkflowStatus
{
	MissingNpc,
	KiskRemoved,
	Scheduled,
}
