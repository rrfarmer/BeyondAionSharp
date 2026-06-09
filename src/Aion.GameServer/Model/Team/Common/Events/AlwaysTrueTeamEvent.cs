using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>Java parity: model/team/common/events/AlwaysTrueTeamEvent (ATracer).</summary>
public abstract class AlwaysTrueTeamEvent : ITeamEvent
{
    public bool CheckCondition()
    {
        return true;
    }

    public abstract void HandleEvent();
}
