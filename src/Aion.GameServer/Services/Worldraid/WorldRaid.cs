using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Worldraid;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Worldraid;

/// <summary>Java parity: services/worldraid/WorldRaid (Whoop, Sykra). A single world-raid instance: timed preparation sequence (flag@0, vortex@10, markers@25, spawn-msg@29, random boss@30 via fixed-rate task), boss death observer -> stop raid, 1h boss despawn timer, spawn/despawn helpers, broadcast. AtomicBoolean start/finish gates; anonymous stateful Runnable (progress) -> nested PreparationRunnable capturing outer; Future->ScheduledTask; scheduleAtFixedRate(Runnable,0,60000) faithful; schedule(...,1,HOURS)->Schedule(ct-lambda, TimeSpan.FromHours(1)); DeathObserver lambda. SpawnEngine/WorldRaidService/templates red-tolerated.</summary>
public class WorldRaid
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(WorldRaid));

    private readonly WorldRaidLocation raidLocation;
    private readonly bool useSpecialSpawnMsg;
    private readonly bool sendMessages;
    private readonly AtomicBoolean isFinished = new AtomicBoolean();
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    private WorldRaidNpc randomBossTemplate;
    private Npc boss, flag, vortex;
    private List<Npc> locationMarkers = new List<Npc>();
    private ScheduledTask stopRaidTask, preparationTask;

    public WorldRaid(WorldRaidLocation raidLocation, bool useSpecialSpawnMsg, bool sendMessages)
    {
        this.raidLocation = raidLocation;
        this.useSpecialSpawnMsg = useSpecialSpawnMsg;
        this.sendMessages = sendMessages;
    }

    public void StartWorldRaid()
    {
        if (isStarted.CompareAndSet(false, true))
            OnWorldRaidStart();
    }

    public void StopWorldRaid()
    {
        if (isFinished.CompareAndSet(false, true))
            OnWorldRaidFinish();
    }

    private void OnWorldRaidStart()
    {
        if (preparationTask != null)
            preparationTask.Cancel(false);
        preparationTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new PreparationRunnable(this), 0, 60000);
    }

    private void OnWorldRaidFinish()
    {
        DespawnNpcs(flag, vortex, boss);
        DespawnNpcs(locationMarkers);
    }

    private void ScheduleBossDespawn()
    {
        stopRaidTask = ThreadPoolManager.GetInstance().Schedule(ct => { WorldRaidService.GetInstance().StopRaid(GetLocationId()); return ValueTask.CompletedTask; }, TimeSpan.FromHours(1));
    }

    private void CancelStopRaidTask()
    {
        if (stopRaidTask != null && !stopRaidTask.IsCancelled())
        {
            stopRaidTask.Cancel(false);
            stopRaidTask = null;
        }
    }

    private void DespawnNpcs(params Npc[] npcs)
    {
        foreach (Npc npc in npcs)
            npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    private void DespawnNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
            npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    private void SpawnAndInitRandomBoss()
    {
        randomBossTemplate = Rnd.Get(raidLocation.GetNpcPool());
        SpawnTemplate bossTemplate = SpawnEngine.NewSingleTimeSpawn(raidLocation.GetMapId(), randomBossTemplate.GetNpcId(), raidLocation.GetX(),
            raidLocation.GetY(), raidLocation.GetZ(), raidLocation.GetH(), null, "world_raid_aggressive");
        Npc bossNpc = (Npc)SpawnEngine.SpawnObject(bossTemplate, 1);
        if (bossNpc == null)
        {
            log.LogWarning("Cannot initialize world raid boss with ID " + randomBossTemplate.GetNpcId() + ". No boss was spawned.");
            return;
        }
        boss = bossNpc;
        RegisterDeathObserver(boss);
    }

    private void RegisterDeathObserver(Npc npc)
    {
        npc.GetObserveController().Attach(new DeathObserver(_ =>
        {
            if (IsFinished())
                return;
            if (randomBossTemplate.GetDeathMsgId() != null) // STR_MSG_WORLDRAID_MESSAGE_DIE_01-06
                BroadcastMessage(new SM_SYSTEM_MESSAGE(randomBossTemplate.GetDeathMsgId().Value), true);
            CancelStopRaidTask();
            WorldRaidService.GetInstance().StopRaid(GetLocationId());
        }));
    }

    private void SpawnAndInitMapFlag()
    {
        SpawnTemplate flagTemplate = SpawnEngine.NewSingleTimeSpawn(raidLocation.GetMapId(), 832819, raidLocation.GetX(), raidLocation.GetY(),
            raidLocation.GetZ(), (byte)0);
        flag = (Npc)SpawnEngine.SpawnObject(flagTemplate, 1);
    }

    private void SpawnAndInitVortex()
    {
        SpawnTemplate vortexTemplate = SpawnEngine.NewSingleTimeSpawn(raidLocation.GetMapId(), 702550, raidLocation.GetX(), raidLocation.GetY(),
            raidLocation.GetZ() + 40f, (byte)0);
        vortex = (Npc)SpawnEngine.SpawnObject(vortexTemplate, 1);
    }

    private void SpawnAndInitMarkerSpots()
    {
        foreach (MarkerSpot locationMarker in raidLocation.GetLocationMarkers())
        {
            SpawnTemplate markerTemplate = SpawnEngine.NewSingleTimeSpawn(raidLocation.GetMapId(), 702548, locationMarker.GetX(), locationMarker.GetY(),
                locationMarker.GetZ(), locationMarker.GetH());
            locationMarkers.Add((Npc)SpawnEngine.SpawnObject(markerTemplate, 1));
        }
    }

    private void BroadcastMessage(SM_SYSTEM_MESSAGE msg)
    {
        BroadcastMessage(msg, false);
    }

    private void BroadcastMessage(SM_SYSTEM_MESSAGE msg, bool forceMsg)
    {
        if (msg != null && (sendMessages || forceMsg))
            World.GetInstance().GetWorldMap(raidLocation.GetMapId()).GetMainWorldMapInstance().ForEachPlayer(p => PacketSendUtility.SendPacket(p, msg));
    }

    public int GetLocationId()
    {
        return raidLocation.GetLocationId();
    }

    public bool IsFinished()
    {
        return isFinished.Get();
    }

    private sealed class PreparationRunnable : Runnable
    {
        private readonly WorldRaid outer;
        private int progress = 0;

        public PreparationRunnable(WorldRaid outer)
        {
            this.outer = outer;
        }

        public void Run()
        {
            switch (progress++)
            {
                case 0:
                    outer.SpawnAndInitMapFlag();
                    outer.BroadcastMessage(SM_SYSTEM_MESSAGE.STR_MSG_WORLDRAID_MESSAGE_01());
                    break;
                case 10: // 10 minutes
                    outer.SpawnAndInitVortex();
                    outer.BroadcastMessage(SM_SYSTEM_MESSAGE.STR_MSG_WORLDRAID_MESSAGE_02());
                    break;
                case 25: // 25 minutes
                    outer.SpawnAndInitMarkerSpots();
                    outer.BroadcastMessage(SM_SYSTEM_MESSAGE.STR_MSG_WORLDRAID_MESSAGE_03());
                    break;
                case 29: // 29 minutes
                    if (!EventsConfig.WORLDRAID_ENABLE_SPAWNMSG)
                        break;
                    if (outer.useSpecialSpawnMsg)
                        outer.BroadcastMessage(SM_SYSTEM_MESSAGE.STR_MSG_WORLDRAID_INVADE_VRITRA_SPECIAL());
                    else
                        outer.BroadcastMessage(SM_SYSTEM_MESSAGE.STR_MSG_WORLDRAID_INVADE_VRITRA());
                    break;
                case 30: // 30 minutes
                    outer.preparationTask.Cancel(false);
                    outer.preparationTask = null;
                    outer.DespawnNpcs(outer.vortex);
                    outer.SpawnAndInitRandomBoss();
                    outer.BroadcastMessage(SM_SYSTEM_MESSAGE.STR_MSG_WORLDRAID_MESSAGE_04());
                    outer.ScheduleBossDespawn();
                    break;
            }
        }
    }
}
