using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerMovementBroadcastService
{
	private readonly GameWorld _world;
	private readonly IGameClientConnectionRegistry _connectionRegistry;

	public WorldNpcWalkerMovementBroadcastService(
		GameWorld world,
		IGameClientConnectionRegistry connectionRegistry)
	{
		_world = world;
		_connectionRegistry = connectionRegistry;
	}

	public async Task<WorldNpcWalkerMovementBroadcastResult> BroadcastWalkerMovementAsync(
		int objectId,
		WorldNpcWalkerMovementState movementState,
		byte movementMask = MovementMask.NpcStartMove,
		CancellationToken cancellationToken = default)
	{
		// Java parity: controllers/movement/NpcMoveController.moveToLocation broadcasts SM_MOVE(owner) to sighted players when the NPC target changes.
		cancellationToken.ThrowIfCancellationRequested();
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return new WorldNpcWalkerMovementBroadcastResult(false, 0);

		var packet = new SmMove(npc, movementState, movementMask);
		var sentCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(npc.Position, npc.ObjectId, packet);
		return new WorldNpcWalkerMovementBroadcastResult(true, sentCount);
	}
}

public readonly record struct WorldNpcWalkerMovementBroadcastResult(
	bool Broadcasted,
	int SentCount);
