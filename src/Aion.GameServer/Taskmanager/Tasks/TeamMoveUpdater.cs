using Aion.GameServer.Taskmanager;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>
/// Supports PlayerGroup and PlayerAlliance movement updating.
/// Java parity: taskmanager/tasks/TeamMoveUpdater (Sarynth).
/// </summary>
public sealed class TeamMoveUpdater : AbstractFIFOPeriodicTaskManager<Aion.GameServer.Model.GameObjects.Players.Player>
{
    private static class SingletonHolder
    {
        internal static readonly TeamMoveUpdater INSTANCE = new TeamMoveUpdater();
    }

    public static TeamMoveUpdater GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    public TeamMoveUpdater()
        : base(2000)
    {
    }

    protected override void CallTask(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (player.IsOnline())
        {
            if (player.IsInGroup())
            {
                Aion.GameServer.Model.Team.Group.PlayerGroupService.UpdateGroup(player, Aion.GameServer.Model.Team.Common.Legacy.GroupEvent.MOVEMENT);
            }
            else if (player.IsInAlliance())
            {
                Aion.GameServer.Model.Team.Alliance.PlayerAllianceService.UpdateAlliance(player, Aion.GameServer.Model.GameObjects.PlayerAllianceEvent.MOVEMENT);
            }
        }
    }

    protected override string GetCalledMethodName()
    {
        return "teamMoveUpdate()";
    }
}
