using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskLifetimeService
{
	public static PlayerKiskDespawnResult DespawnExpiredKisk(
		GameWorld world,
		PlayerKiskRegistry registry,
		IDFactory? idFactory,
		int kiskObjectId)
	{
		// Java parity: model/gameobjects/Kisk.KiskLifeTask.run -> KiskController.delete + KiskService.removeKisk.
		if (!registry.TryRemoveKisk(kiskObjectId, out var kisk) || kisk == null)
			return PlayerKiskDespawnResult.NotFound(kiskObjectId);

		var memberObjectIds = kisk.CurrentMemberIds;
		if (!world.TryGetObject(kiskObjectId, out var gameObject) || gameObject is not IWorldNpcObject npc)
			return PlayerKiskDespawnResult.RegistryOnly(kiskObjectId, memberObjectIds);

		if (!world.TryRemoveObject(kiskObjectId, out _))
			return PlayerKiskDespawnResult.RegistryOnly(kiskObjectId, memberObjectIds);

		var releasedObjectId = idFactory?.ReleaseId(kiskObjectId) == true;
		return PlayerKiskDespawnResult.Removed(
			kiskObjectId,
			npc.Position.WorldId,
			memberObjectIds,
			releasedObjectId);
	}
}

public sealed record PlayerKiskDespawnResult(
	int KiskObjectId,
	bool RemovedRegistry,
	bool RemovedWorldObject,
	bool ReleasedObjectId,
	int? WorldId,
	IReadOnlyList<int> MemberObjectIds)
{
	public static PlayerKiskDespawnResult NotFound(int kiskObjectId)
	{
		return new PlayerKiskDespawnResult(kiskObjectId, false, false, false, null, []);
	}

	public static PlayerKiskDespawnResult RegistryOnly(int kiskObjectId, IReadOnlyList<int> memberObjectIds)
	{
		return new PlayerKiskDespawnResult(kiskObjectId, true, false, false, null, memberObjectIds);
	}

	public static PlayerKiskDespawnResult Removed(
		int kiskObjectId,
		int worldId,
		IReadOnlyList<int> memberObjectIds,
		bool releasedObjectId)
	{
		return new PlayerKiskDespawnResult(kiskObjectId, true, true, releasedObjectId, worldId, memberObjectIds);
	}
}
