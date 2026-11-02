using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Java parity: world/zone/ZoneLevelService (ATracer). Checks water level (start drowning).
/// Anonymous Runnable + scheduleAtFixedRate(0, period) -> ScheduleAtFixedRateTask(ct => {...; return ValueTask.CompletedTask;},
/// TimeSpan, TimeSpan) per the reworked async ThreadPoolManager API. World.GetInstance/
/// GetWorldMap red-tolerated (World is a partially-ported god class).
/// </summary>
public class ZoneLevelService
{
    private const long DROWN_PERIOD = 1000;

    /// <summary>Check water level (start drowning)</summary>
    public static void CheckZoneLevels(Player player)
    {
        World world = Aion.GameServer.World.World.GetInstance();
        float z = player.GetZ();

        if (player.IsDead())
            return;

        float defaultEyeHeight = player.GetGender() == Gender.MALE ? 1.7f : 1.58f; // eye_height from client_PCRace.xml
        float eyeHeight = player.GetPlayerAppearance().GetHeightScale() * defaultEyeHeight;
        float mouthHeight = eyeHeight - 0.05f;
        if (z + mouthHeight < world.GetWorldMap(player.GetWorldId()).GetWaterLevel())
            StartDrowning(player);
        else
            StopDrowning(player);
    }

    private static void StopDrowning(Player player)
    {
        if (player.GetController().HasTask(TaskId.DROWN))
            player.GetController().CancelTask(TaskId.DROWN);
    }

    private static void StartDrowning(Player player)
    {
        if (player.GetController().HasTask(TaskId.DROWN))
            return;
        player.GetController().AddTask(TaskId.DROWN, ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            int value = player.GetLifeStats().GetMaxHp() / 20;
            if (player.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.DROWNING, value, 0, SmAttackStatus.LOG.REGULAR, player) == 0)
                StopDrowning(player);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(DROWN_PERIOD)));
    }
}
