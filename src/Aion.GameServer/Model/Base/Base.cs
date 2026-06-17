using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Base;

/// <summary>
/// Java parity: model/base/Base&lt;T extends BaseLocation&gt; (Source, Estrayl).
/// Non-generic base (holds all members; bLoc typed as BaseLocation) — consumers reference this; generic shim below re-types GetLocation().
/// </summary>
public abstract class Base
{
    private readonly BaseLocation bLoc;
    private readonly int id;
    private readonly List<Npc> assaulter = new List<Npc>();
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    private readonly AtomicBoolean isStopped = new AtomicBoolean();
    private ScheduledTask assaultTask, assaultDespawnTask, bossSpawnTask, outriderSpawnTask;
    private Npc flag;

    protected abstract int GetAssaultDelay();

    protected abstract int GetAssaultDespawnDelay();

    protected abstract int GetBossSpawnDelay();

    protected abstract int GetNpcSpawnDelay();

    protected Base(BaseLocation bLoc)
    {
        this.bLoc = bLoc;
        this.id = bLoc.GetId();
    }

    public void Start()
    {
        if (isStarted.CompareAndSet(false, true))
            HandleStart();
        else
            throw new BaseException("Attempt to start Base twice! ID:" + id);
    }

    public void Stop()
    {
        if (isStopped.CompareAndSet(false, true))
            HandleStop();
        else
            throw new BaseException("Attempt to stop Base twice! ID:" + id);
    }

    protected virtual void HandleStart()
    {
        SpawnBySpawnHandler(SpawnHandlerType.FLAG, GetOccupier());
        SpawnBySpawnHandler(SpawnHandlerType.MERCHANT, GetOccupier());
        SpawnBySpawnHandler(SpawnHandlerType.SENTINEL, GetOccupier());
        ScheduleOutriderSpawn();
        ScheduleBossSpawn();
    }

    protected virtual void HandleStop()
    {
        CancelTask(assaultTask, assaultDespawnTask, bossSpawnTask, outriderSpawnTask);
        DespawnAllNpcs();
    }

    private void DespawnAllNpcs()
    {
        DespawnNpcs(null);
    }

    protected void DespawnByHandlerType(SpawnHandlerType type)
    {
        DespawnNpcs(type);
    }

    private void DespawnNpcs(SpawnHandlerType? type)
    {
        Aion.GameServer.World.World.GetInstance().GetWorldMap(GetLocation().GetTemplate().GetWorldId()).ForEachObject(o =>
        {
            if (IsSpawnForCurrentBase(o.GetSpawn(), type))
                o.GetController().DeleteIfAliveOrCancelRespawn();
        });
        RespawnService.CancelRespawns(spawnTemplate => IsSpawnForCurrentBase(spawnTemplate, type));
    }

    private bool IsSpawnForCurrentBase(SpawnTemplate spawnTemplate, SpawnHandlerType? type)
    {
        return spawnTemplate is BaseSpawnTemplate spawn && spawn.GetId() == id && (type == null || spawn.GetHandlerType() == type);
    }

    protected void ScheduleOutriderSpawn()
    {
        outriderSpawnTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (isStopped.Get() || GetNpcSpawnDelay() == 0)
                return ValueTask.CompletedTask;
            SpawnBySpawnHandler(SpawnHandlerType.OUTRIDER, GetOccupier());
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(GetNpcSpawnDelay()));
    }

    protected void ScheduleBossSpawn()
    {
        if (bLoc.GetOccupier() == BaseOccupier.PEACE)
            return; // Peace does not include any boss or the possibility to capture it

        bossSpawnTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (isStopped.Get())
                return ValueTask.CompletedTask;
            SpawnBySpawnHandler(SpawnHandlerType.BOSS, GetOccupier());
            SM_SYSTEM_MESSAGE bossSpawnMsg = GetBossSpawnMsg();
            if (bossSpawnMsg != null)
                PacketSendUtility.BroadcastToMap(flag.GetPosition().GetWorldMapInstance(), bossSpawnMsg);
            ScheduleAssault();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(GetBossSpawnDelay()));
    }

    private void ScheduleAssault()
    {
        if (bLoc.GetType_() == BaseType.PANESTERRA_FACTION_CAMP || bLoc.GetType_() == BaseType.PANESTERRA_ARTIFACT)
            return; // No assault for those two

        assaultTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (isStopped.Get())
                return ValueTask.CompletedTask;
            if (flag.GetPosition().IsMapRegionActive())
            {
                SpawnBySpawnHandler(SpawnHandlerType.ATTACKER, ChooseAssaultRace());
                SM_SYSTEM_MESSAGE assaultMsg = GetAssaultMsg();
                if (assaultMsg != null)
                    PacketSendUtility.BroadcastToMap(flag.GetPosition().GetWorldMapInstance(), assaultMsg);
                ScheduleAssaultDespawn();
            }
            else
            {
                if (Rnd.Chance() < 20)
                    BaseService.GetInstance().Capture(id, ChooseAssaultRace());
                ScheduleAssault();
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(GetAssaultDelay()));
    }

    protected virtual BaseOccupier ChooseAssaultRace()
    {
        List<BaseOccupier> coll = new List<BaseOccupier> { BaseOccupier.ASMODIANS, BaseOccupier.ELYOS, BaseOccupier.BALAUR };
        coll.Remove(GetOccupier());
        return Rnd.Get(coll);
    }

    private void ScheduleAssaultDespawn()
    {
        assaultDespawnTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (isStopped.Get())
                return ValueTask.CompletedTask;
            DespawnAssaulter();
            ScheduleAssault();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(GetAssaultDespawnDelay()));
    }

    private void DespawnAssaulter()
    {
        foreach (Npc npc in assaulter)
            npc.GetController().DeleteIfAliveOrCancelRespawn();
        assaulter.Clear();
    }

    public void SpawnBySpawnHandler(SpawnHandlerType type, BaseOccupier occupier)
    {
        Npc boss = null;
        foreach (SpawnGroup group in DataManager.SPAWNS_DATA.GetBaseSpawnsByLocId(id))
        {
            if (group.GetHandlerType() != type)
                continue;
            foreach (SpawnTemplate template in group.GetSpawnTemplates())
            {
                if (((BaseSpawnTemplate) template).GetOccupier() != occupier)
                    continue;
                Npc npc = (Npc) Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, 1);
                if (npc == null)
                    throw new BaseException("Npc " + template.GetNpcId() + " could not be spawned at base " + id);
                switch (type)
                {
                    case SpawnHandlerType.ATTACKER:
                        assaulter.Add(npc);
                        break;
                    case SpawnHandlerType.BOSS:
                        if (boss != null)
                            throw new BaseException("Tried to spawn boss twice at base " + id);
                        boss = npc;
                        break;
                    case SpawnHandlerType.FLAG:
                        if (flag != null)
                            throw new BaseException("Tried to spawn flag twice at base " + id);
                        flag = npc;
                        break;
                }
            }
        }
        if (type == SpawnHandlerType.BOSS && boss == null)
            throw new BaseException("No boss found for base! ID: " + id);
        if (type == SpawnHandlerType.FLAG && flag == null)
            throw new BaseException("No flag found for base! ID: " + id);
    }

    public virtual BaseOccupier GetOccupier(Creature bossKiller)
    {
        return bossKiller == null ? GetLocation().GetTemplate().GetDefaultOccupier() : BaseOccupierExtensions.FindBy(bossKiller.GetRace());
    }

    private SM_SYSTEM_MESSAGE GetBossSpawnMsg()
    {
        return id switch
        {
            6101 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V01(),
            6102 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V02(),
            6103 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V03(),
            6104 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V04(),
            6105 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V05(),
            6106 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V06(),
            6107 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V07(),
            6108 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V08(),
            6109 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V09(),
            6110 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V10(),
            6111 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V11(),
            6112 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V12(),
            6113 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_CHIEF_V13(),
            _ => null,
        };
    }

    private SM_SYSTEM_MESSAGE GetAssaultMsg()
    {
        return id switch
        {
            6101 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V01(),
            6102 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V02(),
            6103 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V03(),
            6104 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V04(),
            6105 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V05(),
            6106 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V06(),
            6107 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V07(),
            6108 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V08(),
            6109 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V09(),
            6110 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V10(),
            6111 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V11(),
            6112 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V12(),
            6113 => SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_KILLER_V13(),
            _ => null,
        };
    }

    /// <param name="tasks">can be null if the base is captured with command or under npc control</param>
    protected void CancelTask(params ScheduledTask[] tasks)
    {
        foreach (ScheduledTask task in tasks)
        {
            if (task != null && !task.Completion.IsCompleted)
                task.Cancel();
        }
    }

    public BaseLocation GetLocation()
    {
        return bLoc;
    }

    public int GetId()
    {
        return id;
    }

    public int GetWorldId()
    {
        return bLoc.GetWorldId();
    }

    public BaseOccupier GetOccupier()
    {
        return bLoc.GetOccupier();
    }

    public bool IsStarted()
    {
        return isStarted.Get();
    }

    public bool IsStopped()
    {
        return isStopped.Get();
    }

    public bool IsUnderAssault()
    {
        return assaulter.Count != 0;
    }
}

/// <summary>Generic shim: re-types GetLocation() to T. Java parity: Base&lt;T extends BaseLocation&gt;.</summary>
public abstract class Base<T> : Base where T : BaseLocation
{
    protected Base(T bLoc)
        : base(bLoc)
    {
    }

    public new T GetLocation()
    {
        return (T) base.GetLocation();
    }
}
