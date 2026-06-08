using System.Collections.Generic;

namespace Aion.GameServer.GeoEngine.Collision;

/// <summary>
/// Java parity: geoEngine/collision/CollisionIntention (Rolandas).
/// Java stores the id as a signed <c>byte</c>; <see cref="CollisionIntentions.GetId"/> returns
/// <c>sbyte</c> so the int sign-extension in the flag tests matches Java exactly (e.g.
/// PHYSICAL_SEE_THROUGH = 1&lt;&lt;7 becomes -128, not 128).
/// </summary>
public enum CollisionIntention
{
    NONE = 0,
    PHYSICAL = 1 << 0, // Physical collision
    MATERIAL = 1 << 1, // Mesh materials with skills
    SKILL = 1 << 2, // Skill obstacles
    WALK = 1 << 3, // Walk/NoWalk obstacles
    DOOR = 1 << 4, // Doors which have a state opened/closed
    EVENT = 1 << 5, // Appear on event only
    MOVEABLE = 1 << 6, // Ships, shugo boxes
    PHYSICAL_SEE_THROUGH = 1 << 7,
    DEFAULT_COLLISIONS = PHYSICAL | DOOR | PHYSICAL_SEE_THROUGH,
    CANT_SEE_COLLISIONS = PHYSICAL | DOOR,

    // This is used for nodes only, means they allow to enumerate their child geometries.
    // Nodes which do not specify it won't let their children enumerated for collisions, to speed up processing.
    ALL = PHYSICAL | MATERIAL | SKILL | WALK | DOOR | EVENT | MOVEABLE | PHYSICAL_SEE_THROUGH,
}

/// <summary>
/// Static helpers for <see cref="CollisionIntention"/> (Java enum statics → C# helper class).
/// </summary>
public static class CollisionIntentions
{
    // declaration order, matching Java values()
    private static readonly CollisionIntention[] Values =
    {
        CollisionIntention.NONE, CollisionIntention.PHYSICAL, CollisionIntention.MATERIAL, CollisionIntention.SKILL,
        CollisionIntention.WALK, CollisionIntention.DOOR, CollisionIntention.EVENT, CollisionIntention.MOVEABLE,
        CollisionIntention.PHYSICAL_SEE_THROUGH, CollisionIntention.DEFAULT_COLLISIONS, CollisionIntention.CANT_SEE_COLLISIONS,
        CollisionIntention.ALL,
    };

    // Java parity: getId() returns the signed byte id.
    public static sbyte GetId(this CollisionIntention intention) => (sbyte)(int)intention;

    // Java parity: getFlagsFromValue(int value)
    public static ISet<CollisionIntention> GetFlagsFromValue(int value)
    {
        var result = new HashSet<CollisionIntention>();
        foreach (CollisionIntention m in Values)
        {
            if ((value & m.GetId()) == m.GetId())
            {
                if (m == CollisionIntention.NONE || m == CollisionIntention.ALL)
                    continue;
                result.Add(m);
            }
        }
        return result;
    }

    // Java parity: toString(int value)
    public static string ToString(int value)
    {
        string str = "";
        foreach (CollisionIntention m in Values)
        {
            if (m == CollisionIntention.NONE || m == CollisionIntention.ALL)
                continue;
            if ((value & m.GetId()) == m.GetId())
            {
                str += m.ToString();
                str += ", ";
            }
        }
        if (str.Length > 0)
            str = str.Substring(0, str.Length - 2);
        return str;
    }
}
