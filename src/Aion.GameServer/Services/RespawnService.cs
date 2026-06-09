using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/RespawnService (ATracer, Source, xTz, Neon).</summary>
public class RespawnService
{
    public const int IMMEDIATE_DECAY = 2 * 1000;
    public const int WITH_DROP_DECAY = 5 * 60 * 1000;
    private static readonly ConcurrentDictionary<int, RespawnTask> pendingRespawns = new ConcurrentDictionary<int, RespawnTask>();
    private static readonly ILogger log = NullLogger.Instance;

    /// <summary>Schedules decay (despawn) of the npc with the default delay time. Replaces an existing decay task.</summary>
    public static ScheduledTask ScheduleDecayTask(Npc npc)
    {
        ISet<Aion.GameServer.Model.Drop.DropItem> drop = Aion.GameServer.Services.Drop.DropRegistrationService.GetInstance().GetCurrentDropMap()[npc.GetObjectId()];
        int decayInterval;
        if (drop == null || drop.Count == 0)
            decayInterval = IMMEDIATE_DECAY;
        else
            decayInterval = WITH_DROP_DECAY;
        return ScheduleDecayTask(npc, decayInterval);
    }

    /// <summary>Schedules decay (despawn) of the object with the specified delay. Replaces an existing creature decay task.</summary>
    public static ScheduledTask ScheduleDecayTask(VisibleObject visibleObject, long delay)
    {
        if (delay == 0)
            delay = IMMEDIATE_DECAY; // always delay, to show death animation
        DecayTask decayTask = new DecayTask(visibleObject.GetObjectId());
        ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            decayTask.Run();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay));
        if (visibleObject is Creature)
            ((Creature)visibleObject).GetController().AddTask(TaskId.DECAY, task);
        return task;
    }

    /// <summary>
    /// Schedules respawn of the object. Objects without respawn time or spawn templates will not respawn.
    /// </summary>
    /// <returns>The task, if the respawn task was initiated, else null.</returns>
    public static RespawnTask ScheduleRespawn(VisibleObject visibleObject)
    {
        SpawnTemplate spawnTemplate = visibleObject.GetSpawn();
        if (spawnTemplate == null || spawnTemplate.IsNoRespawn())
            return null;
        RespawnTask respawnTask = new RespawnTask(visibleObject);
        respawnTask.future = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            respawnTask.Run();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(spawnTemplate.GetRespawnTime() * 1000));
        pendingRespawns.TryGetValue(visibleObject.GetObjectId(), out RespawnTask oldRespawnTask);
        pendingRespawns[visibleObject.GetObjectId()] = respawnTask;
        if (oldRespawnTask != null) // objectId should not have been in pendingRespawns
        {
            if (spawnTemplate == oldRespawnTask.spawnTemplate)
            {
                log.LogWarning(new System.InvalidOperationException(), "Duplicate respawn task initiated for " + visibleObject);
            }
            else
            {
                log.LogWarning("ObjectId " + visibleObject.GetObjectId()
                    + " got released and reassigned while there was a still active respawn task for the old objectId owner. Old owner: Npc ID: "
                    + oldRespawnTask.spawnTemplate.GetNpcId() + ", map ID: " + oldRespawnTask.spawnTemplate.GetWorldId() + ", New owner: " + visibleObject);
            }
        }
        return respawnTask;
    }

    public static bool HasRespawnTask(VisibleObject visibleObject)
    {
        return pendingRespawns.ContainsKey(visibleObject.GetObjectId());
    }

    public static bool SetAutoReleaseId(int objectId)
    {
        if (pendingRespawns.TryGetValue(objectId, out RespawnTask respawn) && respawn != null)
            return respawn.SetReleaseIdOnCompletion();
        return false;
    }

    public static void CancelRespawn(VisibleObject @object)
    {
        CancelRespawn(@object.GetObjectId(), @object.GetSpawn());
    }

    /// <summary>Cancels the respawn for the given objectId only if it also matches the given spawn template.</summary>
    public static bool CancelRespawn(int objectId, SpawnTemplate spawnTemplate)
    {
        if (pendingRespawns.TryGetValue(objectId, out RespawnTask respawnTask) && respawnTask != null && respawnTask.future != null && respawnTask.spawnTemplate == spawnTemplate)
        {
            respawnTask.Cancel();
            return true;
        }
        return false;
    }

    public static int CancelRespawns(Predicate<SpawnTemplate> predicate)
    {
        int count = 0;
        foreach (RespawnTask respawn in pendingRespawns.Values)
        {
            if (predicate(respawn.spawnTemplate))
            {
                respawn.Cancel();
                count++;
            }
        }
        return count;
    }

    public static int CancelEventRespawns(Aion.GameServer.Model.Templates.Event.EventTemplate eventTemplate)
    {
        return CancelRespawns(spawnTemplate => eventTemplate.Equals(spawnTemplate.GetEventTemplate()));
    }

    private class DecayTask
    {
        private readonly int objectId;

        internal DecayTask(int objectId)
        {
            this.objectId = objectId;
        }

        public void Run()
        {
            VisibleObject visibleObject = Aion.GameServer.World.World.GetInstance().FindVisibleObject(objectId);
            if (visibleObject != null)
            {
                visibleObject.GetController().Delete();
            }
        }
    }

    public class RespawnTask
    {
        internal readonly SpawnTemplate spawnTemplate;
        private readonly int instanceId;
        private readonly int oldObjectId;
        internal ScheduledTask future;
        private bool releaseIdOnUnregister;

        public RespawnTask(VisibleObject @object)
        {
            this.spawnTemplate = @object.GetSpawn();
            this.instanceId = @object.GetInstanceId();
            this.oldObjectId = @object.GetObjectId(); // ID of corpse or already despawned object
        }

        public void Run()
        {
            if (TryRegisterOnEventEndTask())
            {
                future = null;
                return;
            }
            Unregister();
            Respawn();
        }

        private bool TryRegisterOnEventEndTask()
        {
            if (spawnTemplate.IsEventSpawn())
                return false;
            foreach (Aion.GameServer.Services.Event.Event activeEvent in Aion.GameServer.Services.Event.EventService.GetInstance().GetActiveEvents())
            {
                if (activeEvent.GetEventTemplate().GetSpawns() == null)
                    continue;
                // if a currently active event contains an event spawn with custom="true" for this non-event spawn, register it for respawn when the event ends
                if (activeEvent.GetEventTemplate().GetSpawns().GetTemplates().Any(m => m.GetMapId() == spawnTemplate.GetWorldId() && m.GetSpawns().Any(spawn => spawn.GetNpcId() == spawnTemplate.GetNpcId() && spawn.IsCustom())))
                    return activeEvent.AddOnEventEndTask(this);
            }
            return false;
        }

        private void Respawn()
        {
            if (!Aion.GameServer.Services.Instance.InstanceService.InstanceExists(spawnTemplate.GetWorldId(), instanceId))
                return;

            VisibleObject respawn = Aion.GameServer.Spawnengine.SpawnEngine.SpawnObject(spawnTemplate.HasPool() ? spawnTemplate.ChangeTemplate(instanceId) : spawnTemplate, instanceId);
            if (respawn != null)
            {
                Aion.GameServer.Services.RiftService.GetInstance().UpdateSpawned(oldObjectId, respawn);
                if (respawn.GetSpawn().IsTemporarySpawn() && respawn.GetObjectId() != oldObjectId)
                    Aion.GameServer.Spawnengine.TemporarySpawnEngine.UnregisterSpawned(oldObjectId);
            }
        }

        internal bool SetReleaseIdOnCompletion()
        {
            lock (this)
            {
                if (this.Equals(pendingRespawns.TryGetValue(oldObjectId, out RespawnTask t) ? t : null)) // unregistering not yet happened
                {
                    releaseIdOnUnregister = true;
                    return true;
                }
            }
            return false;
        }

        private void OnUnregister()
        {
            if (releaseIdOnUnregister)
                Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().ReleaseId(oldObjectId);
        }

        public void Cancel()
        {
            Unregister();
            future.Cancel();
        }

        private void Unregister()
        {
            lock (this)
            {
                if (((ICollection<KeyValuePair<int, RespawnTask>>)pendingRespawns).Remove(new KeyValuePair<int, RespawnTask>(oldObjectId, this)))
                    OnUnregister();
            }
        }
    }
}
