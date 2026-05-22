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
