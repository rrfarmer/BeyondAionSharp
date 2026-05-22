using Aion.GameServer.Model.GameObjects;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class NpcDialogTargetingService
{
	public static NpcDialogTargetingResult ValidateTargetingNpcWithFunction(
		Player player,
		int objectId,
		int dialogActionId,
		GameWorld? world)
	{
		// Java parity: model/gameobjects/player/Player.isTargetingNpcWithFunction.
		if (objectId <= 0 || player.TargetObjectId != objectId)
			return NpcDialogTargetingResult.NotTargeted;

		if (world == null || !world.TryGetObject(objectId, out var gameObject) || gameObject is not IWorldNpcObject npc)
			return NpcDialogTargetingResult.UnknownTarget;

		if (!WorldVisibility.IsVisibleTo(player, npc.Position))
			return NpcDialogTargetingResult.NotVisible;

		return npc.Template.SupportsDialogAction(dialogActionId)
			? NpcDialogTargetingResult.Valid
			: NpcDialogTargetingResult.UnsupportedAction;
	}
}

public enum NpcDialogTargetingResult
{
	Valid,
	NotTargeted,
	UnknownTarget,
	NotVisible,
	UnsupportedAction,
}
