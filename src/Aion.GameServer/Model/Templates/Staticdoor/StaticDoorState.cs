using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.Templates.Staticdoor;

/// <summary>
/// Java parity: model/templates/staticdoor/StaticDoorState (Rolandas). Java enum with per-instance flag field +
/// static EnumSet helpers → C# enum + extension/static methods (EnumSet&lt;StaticDoorState&gt; → ISet&lt;StaticDoorState&gt;).
/// </summary>
public enum StaticDoorState
{
    NONE,
    OPENED,
    CLICKABLE,
    CLOSEABLE,
    ONEWAY
}

public static class StaticDoorStateExtensions
{
    public static int GetFlag(this StaticDoorState s) => s switch
    {
        StaticDoorState.NONE => 0,
        StaticDoorState.OPENED => 1 << 0,
        StaticDoorState.CLICKABLE => 1 << 1,
        StaticDoorState.CLOSEABLE => 1 << 2,
        StaticDoorState.ONEWAY => 1 << 3,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static void SetStates(int flags, ISet<StaticDoorState> state)
    {
        foreach (StaticDoorState states in Enum.GetValues(typeof(StaticDoorState)))
        {
            if (states == StaticDoorState.NONE)
                continue;
            if ((flags & states.GetFlag()) == 0)
                state.Remove(states);
            else
                state.Add(states);
        }
    }

    public static int GetFlags(ISet<StaticDoorState> doorStates)
    {
        int result = 0;
        foreach (StaticDoorState state in Enum.GetValues(typeof(StaticDoorState)))
        {
            if (state == StaticDoorState.NONE)
                continue;
            if (doorStates.Contains(state))
                result |= state.GetFlag();
        }
        return result;
    }
}
