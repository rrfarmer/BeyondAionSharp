using System;

namespace Aion.GameServer.Model.Instance;

/// <summary>Java parity: model/instance/InstanceScoreType. Per-instance id (non-ordinal) → enum + extension.</summary>
public enum InstanceScoreType
{
    UPDATE_INSTANCE_PROGRESS,
    INIT_PLAYER,
    UPDATE_PLAYER_BUFF_STATUS,
    SHOW_REWARD,
    UPDATE_INSTANCE_BUFFS_AND_SCORE,
    UPDATE_ALL_PLAYER_INFO,
    PLAYER_QUIT,
    UPDATE_RANK,
    UPDATE_FACTION_SCORE
}

public static class InstanceScoreTypeExtensions
{
    public static int GetId(this InstanceScoreType t) => t switch
    {
        InstanceScoreType.UPDATE_INSTANCE_PROGRESS => 2,
        InstanceScoreType.INIT_PLAYER => 3,
        InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS => 4,
        InstanceScoreType.SHOW_REWARD => 5,
        InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE => 6,
        InstanceScoreType.UPDATE_ALL_PLAYER_INFO => 7,
        InstanceScoreType.PLAYER_QUIT => 8,
        InstanceScoreType.UPDATE_RANK => 10,
        InstanceScoreType.UPDATE_FACTION_SCORE => 11,
        _ => throw new ArgumentOutOfRangeException(),
    };
}
