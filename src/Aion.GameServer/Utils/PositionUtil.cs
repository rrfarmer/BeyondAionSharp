using Aion.GameServer.Model.Geometry;
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
    // TODO-backlog F2: getDistance(VisibleObject, float, float, float) — needs VisibleObject
    // TODO-backlog F2: getDistance(VisibleObject, VisibleObject[, boolean]) — needs VisibleObject
    // TODO-backlog F2: isInRange(VisibleObject, VisibleObject, float[, boolean]) — needs VisibleObject
    // TODO-backlog F2: isInRange(VisibleObject, float, float, float, float) — needs VisibleObject
    // TODO-backlog F2: isInRangeLimited(VisibleObject, VisibleObject, float, float) — needs VisibleObject
    // TODO-backlog F2: isInsideAttackCylinder(VisibleObject, VisibleObject, ...) — needs AreaDirections
    // TODO-backlog F3: isInAttackRange(Creature, Creature, float) — needs Creature + CreatureMoveController
    // TODO-backlog F3: calculateMaxCoveredDistance(Creature, long) — needs Creature.getGameStats()
    // TODO-backlog F3: isInTalkRange(Creature, Npc) — needs Creature, Npc
    // TODO-backlog F3: isInTalkRange(Creature, HouseObject) — needs Creature, HouseObject
}
