using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PositionUtilService
{
	public static float CalculateAngleFrom(float obj1X, float obj1Y, float obj2X, float obj2Y)
	{
		// Java parity: utils/PositionUtil.calculateAngleFrom(float, float, float, float)
		// casts Math.toDegrees(Math.atan2(...)) to float and normalizes to [0, 360).
		var angleTarget = (float)(Math.Atan2(obj2Y - obj1Y, obj2X - obj1X) * 180d / Math.PI);
		return NormalizeAngle(angleTarget);
	}

	public static float ConvertHeadingToAngle(byte clientHeading)
	{
		// Java parity: utils/PositionUtil.convertHeadingToAngle(byte)
		// returns normalizeAngle(clientHeading * 3f). Java byte is signed, so preserve
		// that behavior for values above 127 that arrive from unsigned packet/db storage.
		return NormalizeAngle(unchecked((sbyte)clientHeading) * 3f);
	}

	public static byte ConvertAngleToHeading(float angle)
	{
		// Java parity: utils/PositionUtil.convertAngleToHeading(float) truncates angle / 3.
		return (byte)(angle / 3f);
	}

	public static byte GetHeadingTowards(float x, float y, float targetX, float targetY)
	{
		// Java parity: utils/PositionUtil.getHeadingTowards(float, float, float, float).
		return ConvertAngleToHeading(CalculateAngleFrom(x, y, targetX, targetY));
	}

	public static float NormalizeAngle(float angle)
	{
		// Java parity: utils/PositionUtil.normalizeAngle(float).
		if (angle >= 360f)
		{
			angle %= 360f;
		}
		else if (angle < 0f)
		{
			if (angle <= -360f)
				angle %= 360f;
			if (angle < 0f)
				angle += 360f;
		}

		return angle;
	}

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
