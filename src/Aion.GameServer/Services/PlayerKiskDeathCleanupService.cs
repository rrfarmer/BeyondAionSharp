using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskDeathCleanupService
{
	public static PlayerKiskDespawnResult? TryRemoveDiedKisk(
		IWorldNpcObject? npc,
		GameWorld? world,
		PlayerKiskRegistry? registry,
		IDFactory? idFactory)
	{
		// Java parity: model/gameobjects/Kisk.isActive becomes false when the NPC is dead,
		// and KiskController.delete flows through KiskService.removeKisk for runtime cleanup.
		if (npc == null || world == null || registry == null || registry.GetKiskState(npc.ObjectId) == null)
			return null;

		return PlayerKiskLifetimeService.DespawnExpiredKisk(world, registry, idFactory, npc.ObjectId);
	}
}
