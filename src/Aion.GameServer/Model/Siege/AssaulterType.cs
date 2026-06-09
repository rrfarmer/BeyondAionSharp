using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.Siege;

/// <summary>
/// Java parity: model/siege/AssaulterType (Estrayl). Java enum with per-instance spawnStake/spawnCosts fields
/// → C# enum + extension accessors.
/// </summary>
public enum AssaulterType
{
    TELEPORT,
    COMMANDER,
    FIGHTER,
    ASSASSIN,
    RANGER,
    WITCH,
    PRIEST,
    GUNNER
}

public static class AssaulterTypeExtensions
{
    public static float GetSpawnStake(this AssaulterType t) => t switch
    {
        AssaulterType.TELEPORT => 0f,
        AssaulterType.COMMANDER => 0f,
        AssaulterType.FIGHTER => 0.3f,
        AssaulterType.ASSASSIN => 0.1f,
        AssaulterType.RANGER => 0.2f,
        AssaulterType.WITCH => 0.15f,
        AssaulterType.PRIEST => 0.1f,
        AssaulterType.GUNNER => 0.15f,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static List<float> GetSpawnCosts(this AssaulterType t) => t switch
    {
        AssaulterType.TELEPORT => new List<float>(),
        AssaulterType.COMMANDER => new List<float> { 1.0f, 1.25f, 1.5f, 1.75f, 2.0f },
        AssaulterType.FIGHTER => new List<float> { 0.2f, 0.4f, 0.8f, 1.0f },
        AssaulterType.ASSASSIN => new List<float> { 0.2f, 0.4f, 0.8f, 1.0f },
        AssaulterType.RANGER => new List<float> { 0.4f, 0.8f, 1.6f, 2.0f },
        AssaulterType.WITCH => new List<float> { 0.5f, 1.0f, 2.0f, 2.5f },
        AssaulterType.PRIEST => new List<float> { 0.6f, 1.2f, 2.4f, 3.0f },
        AssaulterType.GUNNER => new List<float> { 0.4f, 0.8f, 1.6f, 2.0f },
        _ => throw new ArgumentOutOfRangeException(),
    };
}
