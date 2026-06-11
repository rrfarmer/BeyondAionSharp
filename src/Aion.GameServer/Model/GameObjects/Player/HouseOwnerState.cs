using System;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/HouseOwnerState (Rolandas). Java enum with per-instance byte id
/// (bit flags) → C# enum + extension. Java byte → sbyte.
/// </summary>
public enum HouseOwnerState
{
    HAS_OWNER,
    SINGLE_HOUSE,
    BIDDING_ALLOWED
}

public static class HouseOwnerStateExtensions
{
    // Java parity: getId() — (byte)(flag & 0xFF).
    public static sbyte GetId(this HouseOwnerState s) => s switch
    {
        HouseOwnerState.HAS_OWNER => 1 << 0,
        HouseOwnerState.SINGLE_HOUSE => 1 << 1,
        HouseOwnerState.BIDDING_ALLOWED => 1 << 2,
        _ => throw new ArgumentOutOfRangeException(),
    };
}
