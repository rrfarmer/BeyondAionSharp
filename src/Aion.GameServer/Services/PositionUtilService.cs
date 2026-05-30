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

	public static double GetDistance2D(float x1, float y1, float x2, float y2)
	{
		// Java parity: utils/PositionUtil.getDistance(float, float, float, float).
		float dx = x2 - x1;
		float dy = y2 - y1;
		return Math.Sqrt(dx * dx + dy * dy);
	}

	public static double GetDistance3D(float x1, float y1, float z1, float x2, float y2, float z2)
	{
		// Java parity: utils/PositionUtil.getDistance(float, float, float, float, float, float).
		float dx = x1 - x2;
		float dy = y1 - y2;
		float dz = z1 - z2;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}

	public static bool IsInRange(float x1, float y1, float z1, float x2, float y2, float z2, float range)
	{
		// Java parity: utils/PositionUtil.isInRange(float, float, float, float, float, float, float).
		// Uses squared distance to avoid Math.sqrt — strictly less-than like Java.
		float dx = x1 - x2;
		float dy = y1 - y2;
		float dz = z1 - z2;
		return dx * dx + dy * dy + dz * dz < range * range;
	}

	public static bool IsInRangeLimited(
		float x1, float y1, float z1,
		float x2, float y2, float z2,
		float minRange, float maxRange)
	{
		// Java parity: utils/PositionUtil.isInRangeLimited(VisibleObject, VisibleObject, float, float).
		float dx = x2 - x1;
		float dy = y2 - y1;
		float dz = z2 - z1;
		float distSquared = dx * dx + dy * dy + dz * dz;
		return !(distSquared < minRange * minRange || distSquared > maxRange * maxRange);
	}

	public static float CalculateAngleTowards(float x, float y, byte heading, float targetX, float targetY)
	{
		// Java parity: utils/PositionUtil.calculateAngleTowards(float, float, byte, float, float).
		// Returns angle from -180 to +180: 0 = looking directly at target.
		var angle1 = ConvertHeadingToAngle(heading);
		var angle2 = CalculateAngleFrom(x, y, targetX, targetY);
		var angleDiff = angle1 - angle2;
		if (angleDiff < -180f)
			angleDiff += 360f;
		else if (angleDiff > 180f)
			angleDiff -= 360f;
		return angleDiff;
	}

	public static bool CheckAngleDiff(float angle1, float angle2, float maxAngleDiff)
	{
		// Java parity: utils/PositionUtil.checkAngleDiff. Shortest path between angles.
		var angleDiff = Math.Abs(angle1 - angle2);
		if (angleDiff > 180f)
			angleDiff -= 360f;
		return Math.Abs(angleDiff) <= maxAngleDiff;
	}

	public static bool IsBehind(
		float objectX, float objectY,
		float targetX, float targetY, byte targetHeading,
		float maxAngleDiff = 90f)
	{
		// Java parity: utils/PositionUtil.isBehind(VisibleObject, VisibleObject, float).
		var angle1 = CalculateAngleFrom(objectX, objectY, targetX, targetY);
		var angle2 = ConvertHeadingToAngle(targetHeading);
		return CheckAngleDiff(angle1, angle2, maxAngleDiff);
	}

	public static bool IsInFrontOf(
		float objectX, float objectY,
		float targetX, float targetY, byte targetHeading,
		float maxAngleDiff = 90f)
	{
		// Java parity: utils/PositionUtil.isInFrontOf(VisibleObject, VisibleObject, float).
		if (maxAngleDiff >= 180f)
			return true;
		var angle1 = CalculateAngleFrom(targetX, targetY, objectX, objectY);
		var angle2 = ConvertHeadingToAngle(targetHeading);
		return CheckAngleDiff(angle1, angle2, maxAngleDiff);
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
