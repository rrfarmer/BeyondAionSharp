using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>
/// Java parity: model/team/common/events/ChangeLeaderEvent (ATracer) : AbstractTeamPlayerEvent. Java &lt;T extends
/// TemporaryPlayerTeam&lt;?&gt;&gt; -> where T : class (matches the base; subclasses stay 1:1 with Java). team.ApplyOnMembers /
/// team.GetLeader are GeneralTeam methods, red-tolerated under the relaxed bound. Predicate lambda returns bool to continue iteration.
/// </summary>
public abstract class ChangeLeaderEvent<T> : AbstractTeamPlayerEvent<T>
    where T : class
{
    public ChangeLeaderEvent(T team, Player eventPlayer)
        : base(team, eventPlayer)
    {
    }

    /// <summary>New leader either is null or should be online</summary>
    public override bool CheckCondition()
    {
        return eventPlayer == null || eventPlayer.IsOnline();
    }

    protected void ChangeLeaderToNextAvailablePlayer()
    {
        team.ApplyOnMembers(member =>
        {
            if (member.IsOnline() && !member.Equals(team.GetLeader().GetObject()))
            {
                ChangeLeaderTo(member);
                return false;
            }
            return true;
        });
    }

    protected abstract void ChangeLeaderTo(Player player);
}
