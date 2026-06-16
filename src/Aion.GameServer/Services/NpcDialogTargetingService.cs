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
		// Java parity: model/gameobjects/player/Player.isTargetingNpcWithFunction
		//   VisibleObject target = getTarget();
		//   return target instanceof Npc && target.getObjectId() == objectId
		//       && ((Npc) target).getObjectTemplate().supportsAction(dialogActionId);
		// Java checks the player's current target directly (no world lookup); the faithful
		// VisibleObject store (_allObjects) is reached only via the target reference itself.
		var target = player.GetTarget();
		if (objectId <= 0 || target == null || target.GetObjectId() != objectId)
			return NpcDialogTargetingResult.NotTargeted;

		if (target is not Npc npc)
			return NpcDialogTargetingResult.UnknownTarget;

		return npc.GetObjectTemplate().SupportsAction(dialogActionId)
			? NpcDialogTargetingResult.Valid
			: NpcDialogTargetingResult.UnsupportedAction;
	}
}

public enum NpcDialogTargetingResult
{
	Valid,
	NotTargeted,
	UnknownTarget,
	UnsupportedAction,
}
