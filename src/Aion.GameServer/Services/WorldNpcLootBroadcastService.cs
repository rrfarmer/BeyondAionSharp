using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class WorldNpcLootBroadcastService
{
	private readonly WorldNpcLootService _lootService;
	private readonly IGameClientConnectionRegistry _connectionRegistry;

	public WorldNpcLootBroadcastService(
		WorldNpcLootService lootService,
		IGameClientConnectionRegistry connectionRegistry)
	{
		_lootService = lootService;
		_connectionRegistry = connectionRegistry;
	}

	public ScheduledTask? ScheduleFreeForAllBroadcast(IWorldNpcObject npc, TimeSpan? delay = null)
	{
		// Java parity: services/drop/DropService.scheduleFreeForAll starts the DropNpc free-for-all and broadcasts SM_LOOT_STATUS.LOOT_ENABLE.
		return _lootService.ScheduleFreeForAll(
			npc.ObjectId,
			npc,
			delay,
			onStarted: BroadcastScheduledFreeForAllAsync);
	}

	public async ValueTask<WorldNpcRegisteredDropFanoutResult> StartRegisteredDropFanoutAsync(
		IWorldNpcObject npc,
		TimeSpan? freeForAllDelay = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/drop/DropRegistrationService.registerDrop sends initial LOOT_ENABLE packets, then calls DropService.scheduleFreeForAll.
		var initialLoot = await SendInitialLootEnableAsync(npc.ObjectId, cancellationToken);
		var freeForAllTask = ScheduleFreeForAllBroadcast(npc, freeForAllDelay);
		return new WorldNpcRegisteredDropFanoutResult(
			initialLoot,
			FreeForAllScheduled: freeForAllTask != null,
			freeForAllTask);
	}

	public async ValueTask<WorldNpcInitialLootBroadcastResult> SendInitialLootEnableAsync(
		int npcObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/drop/DropRegistrationService.registerDrop directly sends SM_LOOT_STATUS.LOOT_ENABLE to allowed looters.
		cancellationToken.ThrowIfCancellationRequested();
		var initialLoot = _lootService.CreateInitialLootEnableStatus(npcObjectId);
		if (initialLoot.LootStatus == null || initialLoot.LooterObjectIds.Count == 0)
		{
			return new WorldNpcInitialLootBroadcastResult(
				Broadcasted: false,
				TargetCount: 0,
				SentCount: 0,
				initialLoot.Status);
		}

		var sentCount = 0;
		foreach (var looterObjectId in initialLoot.LooterObjectIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (await _connectionRegistry.SendPacketToPlayerAsync(looterObjectId, initialLoot.LootStatus))
				sentCount++;
		}

		return new WorldNpcInitialLootBroadcastResult(
			Broadcasted: true,
			TargetCount: initialLoot.LooterObjectIds.Count,
			sentCount,
			initialLoot.Status);
	}

	public async ValueTask<WorldNpcLootBroadcastResult> BroadcastFreeForAllAsync(
		WorldNpcFreeForAllResult result,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PacketSendUtility.broadcastPacket(visibleObject, new SM_LOOT_STATUS(...), optional same-race filter).
		cancellationToken.ThrowIfCancellationRequested();
		if (result.Status != WorldNpcFreeForAllStatus.Started
			|| result.Npc == null
			|| result.LootStatus == null)
		{
			return new WorldNpcLootBroadcastResult(Broadcasted: false, SentCount: 0);
		}

		var sentCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			result.Npc.Position,
			result.Npc.ObjectId,
			result.LootStatus,
			filter: result.CanBroadcastTo);
		return new WorldNpcLootBroadcastResult(Broadcasted: true, sentCount);
	}

	private async ValueTask BroadcastScheduledFreeForAllAsync(WorldNpcFreeForAllResult result)
	{
		await BroadcastFreeForAllAsync(result);
	}
}

public readonly record struct WorldNpcLootBroadcastResult(
	bool Broadcasted,
	int SentCount);

public readonly record struct WorldNpcInitialLootBroadcastResult(
	bool Broadcasted,
	int TargetCount,
	int SentCount,
	WorldNpcInitialLootEnableStatus Status);

public sealed record WorldNpcRegisteredDropFanoutResult(
	WorldNpcInitialLootBroadcastResult InitialLoot,
	bool FreeForAllScheduled,
	ScheduledTask? FreeForAllTask);
