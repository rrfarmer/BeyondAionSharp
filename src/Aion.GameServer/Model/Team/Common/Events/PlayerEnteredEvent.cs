using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Services.Event;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>
/// Java parity: model/team/common/events/PlayerEnteredEvent (Neon). Base for "player joined a team" events. Java TeamEvent ->
/// ITeamEvent; bound &lt;T extends TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt;&gt; relaxed to where T : class to keep
/// subclasses 1:1 single-parameter with Java (C# invariant generics reject concrete teams against a TemporaryPlayerTeam&lt;erasure&gt;
/// bound). team.HasMember / EventService.OnEnteredTeam therefore resolve via the concrete T in subclasses; red-tolerated here.
/// checkCondition/handleEvent are virtual so subclasses override and call base.HandleEvent().
/// </summary>
public abstract class PlayerEnteredEvent<T> : ITeamEvent
    where T : TemporaryPlayerTeam
{
    protected readonly T team;
    protected readonly Player player;

    public PlayerEnteredEvent(T team, Player player)
    {
        this.team = team;
        this.player = player;
    }

    /// <summary>Entered player should not be in team yet</summary>
    public virtual bool CheckCondition()
    {
        return !team.HasMember(player.GetObjectId());
    }

    public virtual void HandleEvent()
    {
        EventService.GetInstance().OnEnteredTeam(player, team);
    }
}
