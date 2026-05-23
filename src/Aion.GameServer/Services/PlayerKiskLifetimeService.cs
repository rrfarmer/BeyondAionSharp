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
			return PlayerKiskDespawnResult.RegistryOnly(kisk, memberObjectIds);

		if (!world.TryRemoveObject(kiskObjectId, out _))
			return PlayerKiskDespawnResult.RegistryOnly(kisk, memberObjectIds);

		var releasedObjectId = idFactory?.ReleaseId(kiskObjectId) == true;
		return PlayerKiskDespawnResult.Removed(
			kisk,
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
	IReadOnlyList<int> MemberObjectIds,
	PlayerKiskRuntimeState? RemovedKisk)
{
	public static PlayerKiskDespawnResult NotFound(int kiskObjectId)
	{
		return new PlayerKiskDespawnResult(kiskObjectId, false, false, false, null, [], null);
	}

	public static PlayerKiskDespawnResult RegistryOnly(PlayerKiskRuntimeState kisk, IReadOnlyList<int> memberObjectIds)
	{
		return new PlayerKiskDespawnResult(kisk.ObjectId, true, false, false, null, memberObjectIds, kisk);
	}

	public static PlayerKiskDespawnResult Removed(
		PlayerKiskRuntimeState kisk,
		int worldId,
		IReadOnlyList<int> memberObjectIds,
		bool releasedObjectId)
	{
		return new PlayerKiskDespawnResult(kisk.ObjectId, true, true, releasedObjectId, worldId, memberObjectIds, kisk);
	}
}
