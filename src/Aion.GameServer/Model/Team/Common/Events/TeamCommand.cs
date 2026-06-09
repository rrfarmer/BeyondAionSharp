using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>Java parity: model/team/common/events/TeamCommand (ATracer). Java enum w/ per-instance commandCode + static lookup map → enum + extensions (static ctor builds map).</summary>
public enum TeamCommand
{
    GROUP_BAN_MEMBER,
    GROUP_SET_LEADER,
    GROUP_REMOVE_MEMBER,
    GROUP_SET_LFG, // TODO confirm
    GROUP_START_MENTORING,
    GROUP_END_MENTORING,
    ALLIANCE_LEAVE,
    ALLIANCE_BAN_MEMBER,
    ALLIANCE_SET_CAPTAIN,
    ALLIANCE_CHECKREADY_CANCEL,
    ALLIANCE_CHECKREADY_START,
    ALLIANCE_CHECKREADY_AUTOCANCEL,
    ALLIANCE_CHECKREADY_READY,
    ALLIANCE_CHECKREADY_NOTREADY,
    ALLIANCE_SET_VICECAPTAIN,
    ALLIANCE_UNSET_VICECAPTAIN,
    ALLIANCE_CHANGE_GROUP,
    LEAGUE_LEAVE,
    LEAGUE_EXPEL,
    LEAGUE_ALLIANCE_MOVE,
    LEAGUE_SET_LEADER
}

public static class TeamCommandExtensions
{
    private static readonly Dictionary<int, TeamCommand> teamCommands = new();

    static TeamCommandExtensions()
    {
        foreach (TeamCommand eventCode in Enum.GetValues<TeamCommand>())
        {
            teamCommands[eventCode.GetCodeId()] = eventCode;
        }
    }

    public static int GetCodeId(this TeamCommand e) => e switch
    {
        TeamCommand.GROUP_BAN_MEMBER => 2,
        TeamCommand.GROUP_SET_LEADER => 3,
        TeamCommand.GROUP_REMOVE_MEMBER => 6,
        TeamCommand.GROUP_SET_LFG => 9,
        TeamCommand.GROUP_START_MENTORING => 10,
        TeamCommand.GROUP_END_MENTORING => 11,
        TeamCommand.ALLIANCE_LEAVE => 14,
        TeamCommand.ALLIANCE_BAN_MEMBER => 16,
        TeamCommand.ALLIANCE_SET_CAPTAIN => 17,
        TeamCommand.ALLIANCE_CHECKREADY_CANCEL => 20,
        TeamCommand.ALLIANCE_CHECKREADY_START => 21,
        TeamCommand.ALLIANCE_CHECKREADY_AUTOCANCEL => 22,
        TeamCommand.ALLIANCE_CHECKREADY_READY => 23,
        TeamCommand.ALLIANCE_CHECKREADY_NOTREADY => 24,
        TeamCommand.ALLIANCE_SET_VICECAPTAIN => 25,
        TeamCommand.ALLIANCE_UNSET_VICECAPTAIN => 26,
        TeamCommand.ALLIANCE_CHANGE_GROUP => 27,
        TeamCommand.LEAGUE_LEAVE => 29,
        TeamCommand.LEAGUE_EXPEL => 30,
        TeamCommand.LEAGUE_ALLIANCE_MOVE => 31,
        TeamCommand.LEAGUE_SET_LEADER => 32,
        _ => 0,
    };

    public static TeamCommand GetCommand(int commandCode)
    {
        if (!teamCommands.TryGetValue(commandCode, out var command))
            throw new ArgumentNullException(null, "Invalid team command code " + commandCode);
        return command;
    }
}
