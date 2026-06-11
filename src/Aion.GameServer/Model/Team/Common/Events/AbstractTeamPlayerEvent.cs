using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>
/// Java parity: model/team/common/events/AbstractTeamPlayerEvent (ATracer). Base for team events tied to a single player. Java
/// TeamEvent -> C# ITeamEvent. Java bound &lt;T extends TemporaryPlayerTeam&lt;?&gt;&gt; relaxed to where T : class — the base uses no
/// team API, and this keeps subclasses 1:1 single-parameter with Java (C# can't express the wildcard bound while doing so).
/// handleEvent/checkCondition are implicitly abstract in Java; C# requires explicit abstract re-declaration of the interface members.
/// </summary>
public abstract class AbstractTeamPlayerEvent<T> : ITeamEvent
    where T : class
{
    protected readonly T team;
    protected readonly Player eventPlayer;

    public AbstractTeamPlayerEvent(T team, Player eventPlayer)
    {
        this.team = team;
        this.eventPlayer = eventPlayer;
    }

    public abstract void HandleEvent();

    public abstract bool CheckCondition();
}
