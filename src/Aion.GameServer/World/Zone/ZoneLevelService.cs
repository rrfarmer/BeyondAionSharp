using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Java parity: world/zone/ZoneLevelService (ATracer). Checks water level (start drowning) and map death level (die).
/// Anonymous Runnable + scheduleAtFixedRate(0, period) -> ScheduleAtFixedRateTask(ct => {...; return ValueTask.CompletedTask;},
/// TimeSpan, TimeSpan) per the reworked async ThreadPoolManager API; Math.round(float) -> (int)Math.Floor(x+0.5f). World.GetInstance/
/// GetWorldMap red-tolerated (World is a partially-ported god class).
/// </summary>
public class ZoneLevelService
{
    private const long DROWN_PERIOD = 2000;

    /// <summary>Check water level (start drowning) and map death level (die)</summary>
    public static void CheckZoneLevels(Player player)
    {
        World world = World.GetInstance();
        float z = player.GetZ();

        if (player.IsDead())
            return;

        if (z < world.GetWorldMap(player.GetWorldId()).GetDeathLevel())
        {
            player.GetController().Die();
            return;
        }

        float noseHeight = player.GetPlayerAppearance().GetBoundHeight() - 0.1f;
        if (z + noseHeight < world.GetWorldMap(player.GetWorldId()).GetWaterLevel())
            StartDrowning(player);
        else
            StopDrowning(player);
    }

    private static void StopDrowning(Player player)
    {
        if (player.GetController().HasTask(TaskId.Drown))
            player.GetController().CancelTask(TaskId.Drown);
    }

    private static void StartDrowning(Player player)
    {
        if (player.GetController().HasTask(TaskId.Drown))
            return;
        player.GetController().AddTask(TaskId.Drown, ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            int value = (int)Math.Floor(player.GetLifeStats().GetMaxHp() / 10f + 0.5f);
            if (player.GetLifeStats().ReduceHp(SM_ATTACK_STATUS.TYPE.DROWNING, value, 0, SM_ATTACK_STATUS.LOG.REGULAR, player) == 0)
                StopDrowning(player);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(DROWN_PERIOD)));
    }
}
