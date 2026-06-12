using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.Utils;

/// <summary>
/// Java parity: utils/PositionUtil — basic positional calculations.
/// Only pure coordinate / angle methods are ported here; game-object-aware methods are TODO-backlog
/// pending VisibleObject / Creature / Player (F2-F4).
/// </summary>
public static class PositionUtil
{
    private const float MaxAngleDiff = 90f;

    // ── pure coordinate methods (no VisibleObject dep) ───────────────────────

    /// <summary>Java parity: getDistance(float,float,float,float) — 2D distance.</summary>
    public static double GetDistance(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Java parity: getDistance(Point3D,Point3D).</summary>
    public static double GetDistance(Point3D? point1, Point3D? point2)
    {
        if (point1 == null || point2 == null) return 0;
        return GetDistance(point1.GetX(), point1.GetY(), point1.GetZ(),
                           point2.GetX(), point2.GetY(), point2.GetZ());
    }

    /// <summary>Java parity: getDistance(float,float,float,float,float,float) — 3D distance.</summary>
    public static double GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        float dz = z1 - z2;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Java parity: isInRange(float,float,float,float,float,float,float) — 3D range check without sqrt.</summary>
    public static bool IsInRange(float x1, float y1, float z1, float x2, float y2, float z2, float range)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        float dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz < range * range;
    }

    /// <summary>Java parity: calculateAngleFrom(float,float,float,float) — angle between two points and horizontal axis.</summary>
    public static float CalculateAngleFrom(float obj1X, float obj1Y, float obj2X, float obj2Y)
    {
        float angleTarget = (float)(Math.Atan2(obj2Y - obj1Y, obj2X - obj1X) * (180.0 / Math.PI));
        return NormalizeAngle(angleTarget);
    }

    /// <summary>Java parity: calculateAngleTowards(float,float,byte,float,float).</summary>
    public static float CalculateAngleTowards(float x, float y, byte heading, float targetX, float targetY)
    {
        float angle1 = ConvertHeadingToAngle(heading);
        float angle2 = CalculateAngleFrom(x, y, targetX, targetY);
        float angleDiff = angle1 - angle2;
        if (angleDiff < -180) angleDiff += 360;
        else if (angleDiff > 180) angleDiff -= 360;
        return angleDiff;
    }

    /// <summary>Java parity: convertHeadingToAngle(byte) — client heading → degrees.</summary>
    public static float ConvertHeadingToAngle(byte clientHeading) => NormalizeAngle(clientHeading * 3f);

    /// <summary>Java parity: convertAngleToHeading(float) — degrees → client heading.</summary>
    public static byte ConvertAngleToHeading(float angle) => (byte)(angle / 3);

    /// <summary>Java parity: getHeadingTowards(float,float,float,float).</summary>
    public static byte GetHeadingTowards(float x, float y, float x2, float y2)
        => ConvertAngleToHeading(CalculateAngleFrom(x, y, x2, y2));

    /// <summary>Java parity: getClosestPointOnSegment(float,float,float,float,float,float).</summary>
    public static Point2D GetClosestPointOnSegment(float sx1, float sy1, float sx2, float sy2, float px, float py)
    {
        double xDelta = sx2 - sx1;
        double yDelta = sy2 - sy1;
        if (xDelta == 0 && yDelta == 0)
            throw new ArgumentException("Segment start equals segment end");
        double u = ((px - sx1) * xDelta + (py - sy1) * yDelta) / (xDelta * xDelta + yDelta * yDelta);
        if (u < 0)      return new Point2D(sx1, sy1);
        if (u > 1)      return new Point2D(sx2, sy2);
        return new Point2D((float)(sx1 + u * xDelta), (float)(sy1 + u * yDelta));
    }

    /// <summary>Java parity: normalizeAngle(float) — normalized to [0, 360).</summary>
    public static float NormalizeAngle(float angle)
    {
        if (angle >= 360) angle %= 360;
        else if (angle < 0)
        {
            if (angle <= -360) angle %= 360;
            if (angle < 0) angle += 360;
        }
        return angle;
    }

    // TODO-backlog F2: isBehind(VisibleObject, VisibleObject[, float]) — needs VisibleObject
    // TODO-backlog F2: isInFrontOf(VisibleObject, VisibleObject[, float]) — needs VisibleObject
    // TODO-backlog F2: calculateAngleTowards(VisibleObject, VisibleObject) — needs VisibleObject
    // TODO-backlog F2: calculateAngleFrom(VisibleObject, VisibleObject) — needs VisibleObject
    // TODO-backlog F2: getHeadingTowards(VisibleObject, float, float) — needs VisibleObject
    // TODO-backlog F2: getHeadingTowards(VisibleObject, VisibleObject) — needs VisibleObject
    // TODO-backlog F2: getDirectionalBound(VisibleObject, VisibleObject[, boolean]) — needs VisibleObject.getObjectTemplate().getBoundRadius()
    // Java parity: getDistance(VisibleObject, float, float, float)
    public static double GetDistance(VisibleObject obj, float x, float y, float z) =>
        GetDistance(obj.GetX(), obj.GetY(), obj.GetZ(), x, y, z);

    // Java parity: getDistance(VisibleObject, VisibleObject)
    public static double GetDistance(VisibleObject obj, VisibleObject obj2) => GetDistance(obj, obj2, true);

    // Java parity: getDistance(VisibleObject, VisibleObject, boolean centerToCenter)
    public static double GetDistance(VisibleObject obj, VisibleObject obj2, bool centerToCenter)
    {
        double distance = GetDistance(obj.GetX(), obj.GetY(), obj.GetZ(), obj2.GetX(), obj2.GetY(), obj2.GetZ());
        if (!centerToCenter)
        {
            distance -= obj.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
            distance -= obj2.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
            if (distance < 0)
                distance = 0;
        }
        return distance;
    }

    // Java parity: isInRange(VisibleObject, VisibleObject, float)
    public static bool IsInRange(VisibleObject obj, VisibleObject obj2, float range) => IsInRange(obj, obj2, range, true);

    // Java parity: isInRange(VisibleObject, VisibleObject, float, boolean centerToCenter)
    public static bool IsInRange(VisibleObject obj, VisibleObject obj2, float range, bool centerToCenter)
    {
        if (obj.GetWorldId() != obj2.GetWorldId() || obj.GetInstanceId() != obj2.GetInstanceId())
            return false;
        if (!centerToCenter)
        {
            range += obj.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
            range += obj2.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        }
        return IsInRange(obj.GetX(), obj.GetY(), obj.GetZ(), obj2.GetX(), obj2.GetY(), obj2.GetZ(), range);
    }

    // Java parity: isInRange(VisibleObject, float, float, float, float)
    public static bool IsInRange(VisibleObject obj, float x, float y, float z, float range) =>
        IsInRange(obj.GetX(), obj.GetY(), obj.GetZ(), x, y, z, range);

    // Java parity: isInTalkRange(Creature, Npc)
    public static bool IsInTalkRange(Creature creature, Npc npc)
    {
        float range = npc.GetObjectTemplate().GetTalkDistance() + 1;
        return IsInRange(npc, creature, range, false);
    }

    // Java parity: isInTalkRange(Creature, HouseObject<?>)
    public static bool IsInTalkRange<T>(Creature creature, HouseObject<T> houseObject)
        where T : Aion.GameServer.Model.Templates.Housing.PlaceableHouseObject
    {
        float range = houseObject.GetObjectTemplate().GetTalkingDistance() + 1;
        return IsInRange(houseObject, creature, range, false);
    }

    // Java parity: calculateAngleFrom(VisibleObject, VisibleObject)
    public static float CalculateAngleFrom(VisibleObject obj1, VisibleObject obj2) =>
        CalculateAngleFrom(obj1.GetX(), obj1.GetY(), obj2.GetX(), obj2.GetY());

    // Java parity: checkAngleDiff(float, float, float)
    private static bool CheckAngleDiff(float angle1, float angle2, float maxAngleDiff)
    {
        float angleDiff = Math.Abs(angle1 - angle2);
        if (angleDiff > 180)
            angleDiff -= 360;
        return Math.Abs(angleDiff) <= maxAngleDiff;
    }

    // Java parity: isBehind(VisibleObject, VisibleObject[, float])
    public static bool IsBehind(VisibleObject obj, VisibleObject target) => IsBehind(obj, target, MaxAngleDiff);

    public static bool IsBehind(VisibleObject obj, VisibleObject target, float maxAngleDiff)
    {
        float angle1 = CalculateAngleFrom(obj, target);
        float angle2 = ConvertHeadingToAngle(target.GetHeading());
        return CheckAngleDiff(angle1, angle2, maxAngleDiff);
    }

    // Java parity: isInFrontOf(VisibleObject, VisibleObject[, float])
    public static bool IsInFrontOf(VisibleObject obj, VisibleObject target) => IsInFrontOf(obj, target, MaxAngleDiff);

    public static bool IsInFrontOf(VisibleObject obj, VisibleObject target, float maxAngleDiff)
    {
        if (maxAngleDiff >= 180)
            return true;
        float angle1 = CalculateAngleFrom(target, obj);
        float angle2 = ConvertHeadingToAngle(target.GetHeading());
        return CheckAngleDiff(angle1, angle2, maxAngleDiff);
    }

    // Java parity: calculateMaxCoveredDistance(Creature, long)
    public static float CalculateMaxCoveredDistance(Creature creature, long movementDurationMillis)
    {
        if (movementDurationMillis <= 0)
            return 0;
        int metersPerSecondInThousands = creature.GetGameStats().GetMovementSpeed().GetCurrent();
        return metersPerSecondInThousands * movementDurationMillis / 1_000_000f;
    }

    // Java parity: calculateMaxDistanceOffset(Creature)
    private static float CalculateMaxDistanceOffset(Creature creature)
    {
        float offset = Aion.GameServer.Controllers.Movement.CreatureMoveController.MOVE_CHECK_OFFSET;
        long lastMove = creature.GetMoveController().GetLastMoveUpdate();
        if (lastMove > 0)
        {
            long msSinceLastMove = Math.Min(1000, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastMove);
            offset += CalculateMaxCoveredDistance(creature, msSinceLastMove);
        }
        return offset;
    }

    // Java parity: isInsideAttackCylinder(VisibleObject, VisibleObject, float length, float radius, AreaDirections direction)
    // Tests if obj2 is within a cylinder originating from obj1 with the given length.
    public static bool IsInsideAttackCylinder(VisibleObject obj1, VisibleObject obj2, float length, float radius,
        Aion.GameServer.SkillEngine.Properties.AreaDirections direction)
    {
        double radian = Math.PI / 180.0 * ConvertHeadingToAngle(obj1.GetHeading());
        if (direction == Aion.GameServer.SkillEngine.Properties.AreaDirections.BACK)
            radian += Math.PI;

        length += obj1.GetObjectTemplate().GetBoundRadius().GetFront() + obj2.GetObjectTemplate().GetBoundRadius().GetFront();
        radius += obj2.GetObjectTemplate().GetBoundRadius().GetFront();

        float dx = (float)(Math.Cos(radian) * length);
        float dy = (float)(Math.Sin(radian) * length);

        float tdx = obj2.GetX() - obj1.GetX();
        float tdy = obj2.GetY() - obj1.GetY();
        float tdz = obj2.GetZ() - obj1.GetZ();
        float lengthSqr = length * length;

        float dot = tdx * dx + tdy * dy;
        if (dot < 0.0f || dot > lengthSqr)
            return false;

        // distance squared to the cylinder axis
        return (tdx * tdx + tdy * tdy + tdz * tdz) - (dot * dot / lengthSqr) <= (radius * radius);
    }

    // Java parity: isInAttackRange(Creature, Creature, float)
    public static bool IsInAttackRange(Creature attacker, Creature target, float range)
    {
        if (attacker == null || target == null)
            return false;
        if (attacker.GetMoveController().IsInMove())
        {
            float offset = CalculateMaxDistanceOffset(attacker);
            if (attacker is Player)
                offset *= 1.33f; // client sends inaccurate coordinates during movement
            range += offset;
        }
        if (target.GetMoveController().IsInMove() && attacker is not Npc)
            range += CalculateMaxDistanceOffset(target);
        return IsInRange(attacker, target, range, false);
    }
}
