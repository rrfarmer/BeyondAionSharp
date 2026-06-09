using System;

namespace Aion.GameServer.Model.Instance;

/// <summary>Java parity: model/instance/InstanceProgressionType. Per-instance id (bit values) → enum + extension.</summary>
public enum InstanceProgressionType
{
    REINFORCE_MEMBER,
    PREPARING,
    START_PROGRESS,
    END_PROGRESS
}

public static class InstanceProgressionTypeExtensions
{
    public static int GetId(this InstanceProgressionType t) => t switch
    {
        InstanceProgressionType.REINFORCE_MEMBER => 12 * 1024 * 1024, // 12582912
        InstanceProgressionType.PREPARING => 1 * 1024 * 1024, // 1048576
        InstanceProgressionType.START_PROGRESS => 2 * 1024 * 1024, // 2097152
        InstanceProgressionType.END_PROGRESS => 3 * 1024 * 1024, // 3145728
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static bool IsReinforcing(this InstanceProgressionType t) => t == InstanceProgressionType.REINFORCE_MEMBER;

    public static bool IsPreparing(this InstanceProgressionType t) => t == InstanceProgressionType.PREPARING;

    public static bool IsStartProgress(this InstanceProgressionType t) => t == InstanceProgressionType.START_PROGRESS;

    public static bool IsEndProgress(this InstanceProgressionType t) => t == InstanceProgressionType.END_PROGRESS;
}
