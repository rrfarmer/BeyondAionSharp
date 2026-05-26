using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PositionUtilService
{
	public static bool IsInNpcTalkRange(
		WorldPosition creaturePosition,
		WorldPosition npcPosition,
		float npcTalkDistance,
		float npcBoundRadius,
		float creatureBoundRadius = 0)
	{
		// Java parity: utils/PositionUtil.isInTalkRange(Creature, Npc)
		// delegates to isInRange(npc, creature, talkDistance + 1, false).
		return IsInObjectTalkRange(
			creaturePosition,
			npcPosition,
			npcTalkDistance,
			npcBoundRadius,
			creatureBoundRadius);
	}

	public static bool IsInObjectTalkRange(
		WorldPosition creaturePosition,
		WorldPosition targetPosition,
		float targetTalkingDistance,
		float targetBoundRadius,
		float creatureBoundRadius = 0)
	{
		if (creaturePosition.WorldId != targetPosition.WorldId || creaturePosition.InstanceId != targetPosition.InstanceId)
			return false;

		var range = targetTalkingDistance + 1 + targetBoundRadius + creatureBoundRadius;
		var deltaX = creaturePosition.X - targetPosition.X;
		var deltaY = creaturePosition.Y - targetPosition.Y;
		var deltaZ = creaturePosition.Z - targetPosition.Z;
		return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ < range * range;
	}
}
