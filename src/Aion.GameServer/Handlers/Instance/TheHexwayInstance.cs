using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/TheHexwayInstance (xTz, Bobobear, Sykra) : GeneralInstanceHandler. @InstanceID(300700000).
/// AtomicLongArray→long[] with per-element Interlocked; ConcurrentHashMap→ConcurrentDictionary; Future[]→ScheduledTask[]; 1:1.</summary>
[InstanceID(300700000)]
public class TheHexwayInstance : GeneralInstanceHandler
{
    private const string MSG_TIMER_STARTED = "You have 22 minutes to kill all boss mobs located at the end of each bridge to unlock a bonus chest!";
    private const string MSG_TIMER_NOTIFY = "You have {0} left to unlock the bonus chest.";
    private const string MSG_TIMER_EXPIRED = "Your time to unlock a bonus chest has expired!";
    private const string MSG_BOSS_FAILED_NO_BOX = "You failed to kill a boss mob. No bonus chest will be unlocked.";
    private const string MSG_BOSS_FAILED = "You failed to kill a boss mob.";
    private const string MSG_SUCCESSFUL = "You have successfully completed the instance. A bonus chest was spawned.";

    private readonly int[] bossTimeLimitsSeconds = new int[] { 120, 210, 150, 150, 210, 120 };
    private readonly int[] treasureDoorIds = new int[] { 60, 61, 63, 64, 65, 66 };
    private readonly int[] bossNpcIds = new int[] { 219611, 286933, 219612, 219613, 219610, 219614 };
    private readonly int bonusChestTimeLimitSeconds = 1320;
    private readonly int[] notifyTimesSeconds = new int[] { 900, 600, 300, 120, 60, 30, 10, 3, 2, 1 };

    private readonly ScheduledTask[] scheduledBossDespawnTasks = new ScheduledTask[6];
    private readonly long[] stageStartMillis = new long[6];
    private readonly ConcurrentDictionary<int, int> playerStageMapping = new ConcurrentDictionary<int, int>();

    private readonly AtomicBoolean bonusChestSpawnAllowed = new AtomicBoolean(true);
    private readonly AtomicLong bonusChestStartMillis = new AtomicLong();
    private readonly List<ScheduledTask> timerProgressMsgTasks = new List<ScheduledTask>();
    private readonly AtomicInteger killedBossCount = new AtomicInteger();
    private readonly AtomicInteger attackedBossCount = new AtomicInteger();
    private ScheduledTask disableBonusChestSpawnTask;

    private readonly AtomicBoolean handlingSecondBoss = new AtomicBoolean();

    public TheHexwayInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        InitTimerTrigger();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("HEXWAY_BONUSCHEST"))
        {
            if (bonusChestStartMillis.CompareAndSet(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
            {
                // schedule bonus chest spawn condition change
                disableBonusChestSpawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (bonusChestSpawnAllowed.CompareAndSet(true, false))
                        BroadcastYellowMessage(MSG_TIMER_EXPIRED);
                    return ValueTask.CompletedTask;
                }, bonusChestTimeLimitSeconds * 1000);

                // send info message to all players
                BroadcastYellowMessage(MSG_TIMER_STARTED);

                // schedule timer updates
                foreach (int notifyTimeSecond in notifyTimesSeconds)
                {
                    int scheduleDelaySeconds = bonusChestTimeLimitSeconds - notifyTimeSecond;
                    if (scheduleDelaySeconds > 0)
                    {
                        int captured = notifyTimeSecond;
                        timerProgressMsgTasks.Add(ThreadPoolManager.GetInstance().Schedule(_ =>
                        {
                            SendTimeStringToPlayers(captured);
                            return ValueTask.CompletedTask;
                        }, scheduleDelaySeconds * 1000L));
                    }
                }
            }
        }
        else if (flyingRing.StartsWith("HEXWAY_BOSS_"))
        {
            int bossIndex = int.Parse(flyingRing.Substring(12));
            if (bossIndex >= 0 && bossIndex <= 5)
            {
                if (Interlocked.CompareExchange(ref stageStartMillis[bossIndex], DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0) == 0)
                {
                    attackedBossCount.IncrementAndGet();
                    ScheduleBossDespawn(bossIndex, bossTimeLimitsSeconds[bossIndex] * 1000);
                }
                long stageStartTimeMillis = Volatile.Read(ref stageStartMillis[bossIndex]);
                if (stageStartTimeMillis > 0)
                {
                    int bossTimeLimitSeconds = bossTimeLimitsSeconds[bossIndex];
                    int elapsedTimeMillis = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - stageStartTimeMillis);
                    if (elapsedTimeMillis <= bossTimeLimitSeconds * 1000 && scheduledBossDespawnTasks[bossIndex] != null)
                    {
                        // add player to stage mapping
                        playerStageMapping[player.GetObjectId()] = bossIndex;
                        // send quest timer to player
                        int remainingTimeSeconds = bossTimeLimitSeconds - elapsedTimeMillis / 1000;
                        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, remainingTimeSeconds));
                    }
                }
            }
        }
        return false;
    }

    public override void OnLeaveInstance(Player player)
    {
        base.OnLeaveInstance(player);
        playerStageMapping.TryRemove(player.GetObjectId(), out _);
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 0));
    }

    public override void OnInstanceDestroy()
    {
        // cleanup all scheduled tasks
        foreach (ScheduledTask scheduledBossDespawn in scheduledBossDespawnTasks)
            if (scheduledBossDespawn != null && !scheduledBossDespawn.IsDone())
                scheduledBossDespawn.Cancel(true);

        if (disableBonusChestSpawnTask != null && !disableBonusChestSpawnTask.IsDone())
            disableBonusChestSpawnTask.Cancel(true);
        CancelTimeInformTasks();
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            // manager jarka
            case 219609:
                // open barricades
                DeleteAliveNpcs(219617);
                break;
            case 219611:
            case 286933:
            case 219612:
            case 219613:
            case 219610:
            case 219614:
                // bridge bosses
                for (int bossIndex = 0; bossIndex < bossNpcIds.Length; bossIndex++)
                {
                    int bossNpcId = bossNpcIds[bossIndex];
                    if (npc.GetNpcId() == bossNpcId)
                    {
                        CancelBossDespawn(bossIndex);
                        SpawnBossChest(bossIndex);
                        instance.SetDoorState(treasureDoorIds[bossIndex], true);
                        // remove quest timers for affected players
                        foreach (int playerObjId in new List<int>(playerStageMapping.Keys))
                        {
                            if (playerStageMapping.TryGetValue(playerObjId, out int mappedStage) && mappedStage == bossIndex)
                            {
                                playerStageMapping.TryRemove(playerObjId, out _);
                                Player player = instance.GetPlayer(playerObjId);
                                if (player != null)
                                    PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 0));
                            }
                        }
                        // spawn bonus loot chest if all 6 boss npc are killed in the given time limits
                        if (killedBossCount.IncrementAndGet() == 6 && attackedBossCount.Get() == 6)
                        {
                            if (bonusChestSpawnAllowed.CompareAndSet(true, false))
                            {
                                if (disableBonusChestSpawnTask != null && !disableBonusChestSpawnTask.IsDone())
                                    disableBonusChestSpawnTask.Cancel(true);
                                SpawnBonusChest();
                            }
                            CancelTimeInformTasks();
                        }
                        int remainingTimeSeconds = (int)((bonusChestStartMillis.Get() + bonusChestTimeLimitSeconds * 1000 - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
                        if (remainingTimeSeconds > 0)
                            SendTimeStringToPlayers(remainingTimeSeconds);
                        break;
                    }
                }
                break;
            // handle 2nd bridge boss
            case 219635:
            case 219636:
                if (handlingSecondBoss.CompareAndSet(false, true))
                {
                    int secondNpcId = npc.GetNpcId() == 219635 ? 219636 : 219635;
                    Npc secondNpc = GetNpc(secondNpcId);
                    if (secondNpc != null)
                    {
                        int currentHpPercentage = secondNpc.GetLifeStats().GetHpPercentage();
                        int bossId = bossNpcIds[1];
                        Npc bossNpc = GetNpc(bossId);
                        if (bossNpc != null)
                        {
                            int percentageToDrop = 25 - currentHpPercentage;
                            if (percentageToDrop > 0)
                            {
                                int dmgToApply = (int)(bossNpc.GetLifeStats().GetMaxHp() * (percentageToDrop * 0.8 / 100));
                                ThreadPoolManager.GetInstance().Schedule(_ =>
                                {
                                    if (bossNpc.IsDead() || bossNpc.GetLifeStats().IsAboutToDie())
                                        return ValueTask.CompletedTask;
                                    // Java: bossNpc.getController().onAttack(bossNpc, dmgToApply, null) -> 3-arg expands to full overload with null AttackStatus (C# enum is non-nullable)
                                    bossNpc.GetController().OnAttack(bossNpc, null, Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE.REGULAR, dmgToApply, true, Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG.REGULAR, (Aion.GameServer.Controllers.Attack.AttackStatus?)null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
                                    return ValueTask.CompletedTask;
                                }, 1000);
                            }
                            secondNpc.GetController().Delete();
                        }
                    }
                    handlingSecondBoss.Set(false);
                }
                break;
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, mapId, 672.8343f, 606.2250f, 321.1900f, (byte)0);
        return true;
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        if (npc.GetNpcId() == 700455)
            TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    private void CancelBossDespawn(int bossIndex)
    {
        ScheduledTask scheduledDespawn = scheduledBossDespawnTasks[bossIndex];
        if (scheduledDespawn != null && !scheduledDespawn.IsDone())
            scheduledDespawn.Cancel(true);
        scheduledBossDespawnTasks[bossIndex] = null;
    }

    private void ScheduleBossDespawn(int bossIndex, int despawnDelayMillis)
    {
        ScheduledTask despawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            bool chestSpawnWasAllowed = bonusChestSpawnAllowed.GetAndSet(false);
            DeleteAliveNpcs(bossNpcIds[bossIndex]);
            CancelTimeInformTasks();
            BroadcastYellowMessage(chestSpawnWasAllowed ? MSG_BOSS_FAILED_NO_BOX : MSG_BOSS_FAILED);
            return ValueTask.CompletedTask;
        }, despawnDelayMillis);
        scheduledBossDespawnTasks[bossIndex] = despawnTask;
    }

    private void CancelTimeInformTasks()
    {
        if (timerProgressMsgTasks.Count == 0)
            return;
        for (int i = timerProgressMsgTasks.Count - 1; i >= 0; i--)
        {
            ScheduledTask timerProgressMsgTask = timerProgressMsgTasks[i];
            if (timerProgressMsgTask != null && !timerProgressMsgTask.IsDone())
            {
                timerProgressMsgTask.Cancel(true);
                timerProgressMsgTasks.RemoveAt(i);
            }
        }
    }

    private void SpawnBonusChest()
    {
        BroadcastYellowMessage(MSG_SUCCESSFUL);
        Spawn(701664, 485.59f, 585.42f, 357f, (byte)0);
    }

    private void SpawnBossChest(int bossIndex)
    {
        switch (bossIndex)
        {
            case 0:
                Spawn(701663, 223.6263f, 427.2576f, 364.6045f, (byte)117);
                Spawn(701662, 223.5121f, 409.0381f, 365.0105f, (byte)25);
                break;
            case 1:
                Spawn(701663, 195.7458f, 494.1713f, 365.0105f, (byte)2);
                Spawn(701662, 193.7205f, 499.2636f, 365.0105f, (byte)103);
                break;
            case 2:
                Spawn(701663, 197.9107f, 566.2823f, 365.0105f, (byte)91);
                Spawn(701662, 181.2328f, 537.9518f, 365.0105f, (byte)17);
                break;
            case 3:
                Spawn(701663, 185.4841f, 630.1168f, 365.0105f, (byte)108);
                Spawn(701662, 201.0349f, 612.7051f, 364.6045f, (byte)25);
                break;
            case 4:
                Spawn(701663, 192.4604f, 673.4794f, 365.0105f, (byte)13);
                Spawn(701662, 213.7684f, 696.3926f, 365.0105f, (byte)25);
                break;
            case 5:
                Spawn(701663, 241.1405f, 754.4207f, 365.0105f, (byte)96);
                Spawn(701662, 214.3266f, 743.3648f, 365.0105f, (byte)25);
                break;
        }
    }

    private void InitTimerTrigger()
    {
        SpawnBonusChestTimerTrigger();
        SpawnBossTimerTrigger(0, new Point3D(321.3199, 458.7944, 368.2764), new Point3D(317.8786, 465.5034, 361.7755),
            new Point3D(314.5425, 472.0469, 361.7755));
        SpawnBossTimerTrigger(1, new Point3D(302.9791, 504.9413, 368.2764), new Point3D(300.9405, 511.9926, 361.7755),
            new Point3D(298.7013, 519.5820, 361.7755));
        SpawnBossTimerTrigger(2, new Point3D(293.5730, 553.3477, 368.2764), new Point3D(292.3480, 560.7227, 361.7755),
            new Point3D(291.3300, 568.1430, 361.7755));
        SpawnBossTimerTrigger(3, new Point3D(292.1579, 602.8574, 368.2764), new Point3D(292.4263, 610.3403, 361.7755),
            new Point3D(292.6229, 617.7412, 361.7755));
        SpawnBossTimerTrigger(4, new Point3D(299.2268, 651.8597, 368.2764), new Point3D(300.9197, 659.2391, 361.7755),
            new Point3D(302.4053, 666.5773, 361.7755));
        SpawnBossTimerTrigger(5, new Point3D(315.2073, 698.8103, 368.2764), new Point3D(317.9026, 705.6396, 361.7755),
            new Point3D(320.6522, 712.8006, 361.7755));
    }

    private void SpawnBonusChestTimerTrigger()
    {
        FlyRing f1 = new FlyRing(new FlyRingTemplate("HEXWAY_BONUSCHEST", mapId, new Point3D(576.2102, 585.4146, 353.90677),
            new Point3D(576.2102, 585.4146, 359.90677), new Point3D(575.18384, 596.36664, 353.90677), 10), instance.GetInstanceId());
        f1.Spawn();
    }

    private void SpawnBossTimerTrigger(int number, Point3D p1, Point3D center, Point3D p2)
    {
        FlyRing timerBarrier = new FlyRing(new FlyRingTemplate("HEXWAY_BOSS_" + number, mapId, center, p1, p2, 10), instance.GetInstanceId());
        timerBarrier.Spawn();
    }

    private void BroadcastYellowMessage(string message)
    {
        PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null, message, ChatType.BRIGHT_YELLOW_CENTER));
    }

    private void SendTimeStringToPlayers(int leftTimeSeconds)
    {
        if (bonusChestSpawnAllowed.Get())
            BroadcastYellowMessage(string.Format(MSG_TIMER_NOTIFY, SecondsToReadableString(leftTimeSeconds)));
    }

    private string SecondsToReadableString(int remainingTimeSeconds)
    {
        int remainingMinutes = remainingTimeSeconds / 60;
        int remainingSeconds = remainingTimeSeconds - remainingMinutes * 60;
        string msg;
        if (remainingMinutes == 0)
            msg = string.Format("{0}s", remainingSeconds);
        else if (remainingSeconds == 0)
            msg = string.Format("{0}m", remainingMinutes);
        else
            msg = string.Format("{0}m {1}s", remainingMinutes, remainingSeconds);
        return msg;
    }
}
