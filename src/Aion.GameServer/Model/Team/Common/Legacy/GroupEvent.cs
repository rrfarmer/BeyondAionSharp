using System;

namespace Aion.GameServer.Model.Team.Common.Legacy;

/// <summary>
/// Java parity: model/team/common/legacy/GroupEvent. Per-instance id (ENTER and UPDATE share id 13 but are
/// DISTINCT constants) → C# enum (distinct ordinals) + extension GetId().
/// </summary>
public enum GroupEvent
{
    LEAVE,
    MOVEMENT,
    DISCONNECTED,
    JOIN,
    ENTER_OFFLINE,
    ENTER,
    UPDATE,
    UPDATE_EFFECTS
}

public static class GroupEventExtensions
{
    public static int GetId(this GroupEvent e) => e switch
    {
        GroupEvent.LEAVE => 0,
        GroupEvent.MOVEMENT => 1,
        GroupEvent.DISCONNECTED => 3,
        GroupEvent.JOIN => 5,
        GroupEvent.ENTER_OFFLINE => 7,
        GroupEvent.ENTER => 13,
        GroupEvent.UPDATE => 13,
        GroupEvent.UPDATE_EFFECTS => 65,
        _ => throw new ArgumentOutOfRangeException(),
    };
}
