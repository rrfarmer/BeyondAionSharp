using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Taskmanager;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/TeamStatUpdater (Sarynth) : AbstractFIFOPeriodicTaskManager&lt;Player&gt;. final→sealed; SingletonHolder→nested; callTask/getCalledMethodName→override. Converges PlayerGroupService/PlayerAllianceService.</summary>
public sealed class TeamStatUpdater : AbstractFIFOPeriodicTaskManager<Player>
{
    private static class SingletonHolder
    {
        internal static readonly TeamStatUpdater INSTANCE = new();
    }

    public static TeamStatUpdater GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    public TeamStatUpdater() : base(500)
    {
    }

    protected override void CallTask(Player player)
    {
        if (player.IsOnline())
        {
            if (player.IsInGroup())
            {
                PlayerGroupService.UpdateGroup(player, GroupEvent.MOVEMENT);
            }
            else if (player.IsInAlliance())
            {
                PlayerAllianceService.UpdateAlliance(player, PlayerAllianceEvent.MOVEMENT);
            }
        }
    }

    protected override string GetCalledMethodName()
    {
        return "teamStatUpdate()";
    }
}
