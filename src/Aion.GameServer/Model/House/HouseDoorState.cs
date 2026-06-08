using System;

namespace Aion.GameServer.Model.House;

/// <summary>
/// Java parity: model/house/HouseDoorState (Neon).
/// </summary>
public enum HouseDoorState
{
    OPEN = 1,
    CLOSED_EXCEPT_FRIENDS = 2,
    CLOSED = 3,
}

/// <summary>Static helpers for <see cref="HouseDoorState"/> (Java enum statics).</summary>
public static class HouseDoorStates
{
    // Java parity: getId() (signed byte).
    public static sbyte GetId(this HouseDoorState state) => (sbyte)(int)state;

    // Java parity: get(byte id) — null if not found.
    public static HouseDoorState? Get(sbyte id)
    {
        foreach (HouseDoorState perm in Enum.GetValues<HouseDoorState>())
        {
            if (id == perm.GetId())
                return perm;
        }
        return null;
    }
}
