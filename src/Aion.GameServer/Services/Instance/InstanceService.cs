using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Instance;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services.Instance;

/// <summary>Java parity: services/instance/InstanceService (ATracer) — instance lifecycle/registration. getNextAvailableInstance overloads (Function&lt;WorldMapInstance,IInstanceHandler&gt;->Func, method-group supplier InstanceEngine.GetInstance().GetNewInstanceHandler, event spawn streams->LINQ), destroyInstance, register/get/personal/house instance, login/logout/enter/leave instance+zone hooks, rates. scheduleAtFixedRate EmptyInstanceCheckerTask->nested+async delegate; GeneralTeam&lt;?,?&gt;->GeneralTeam; currentTimeMillis->UtcNow.ToUnixTimeMilliseconds; UnsupportedOperation->NotSupported; NullPointer->NullReference; instanceof Player->is. World/WorldMapInstance/IInstanceHandler/DAO red-tolerated.</summary>
public class InstanceService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(InstanceService));

    public static WorldMapInstance GetNextAvailableInstance(int worldId, int ownerId, byte difficultyId, Func<WorldMapInstance, IInstanceHandler> instanceHandlerSupplier, int maxPlayers, bool autoDestroy)
    {
        WorldMap map = World.World.GetInstance().GetWorldMap(worldId);

        if (!map.IsInstanceType() || map.GetWorldType() == WorldType.PANESTERRA && !map.GetAvailableInstanceIds().IsEmpty())
            throw new NotSupportedException("Invalid call for next available instance  of " + worldId);

        WorldMapInstance instance;
        if (instanceHandlerSupplier == null)
        {
            instance = WorldMapInstanceFactory.CreateWorldMapInstance(map, ownerId, InstanceEngine.GetInstance().GetNewInstanceHandler, maxPlayers);
            Aion.GameServer.SpawnEngine.SpawnEngine.SpawnInstance(instance, difficultyId, ownerId);
        }
        else
        {
            instance = WorldMapInstanceFactory.CreateWorldMapInstance(map, ownerId, instanceHandlerSupplier, maxPlayers);
            EventService.GetInstance().GetActiveEvents().Select(e => e.GetEventTemplate()).Where(t => t.GetSpawns() != null).ToList().ForEach(
                t => Aion.GameServer.SpawnEngine.SpawnEngine.SpawnEventSpawns(instance, difficultyId, ownerId, t));
        }
        instance.GetInstanceHandler().OnInstanceCreate();

        // finally start the checker
        if (autoDestroy)
        {
            EmptyInstanceCheckerTask task = new EmptyInstanceCheckerTask(instance);
            instance.SetEmptyInstanceTask(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(60000), TimeSpan.FromMilliseconds(60000)));
        }

        log.LogInformation("Created new instance: " + worldId + " [" + instance.GetInstanceId() + "] owner:" + ownerId + " difficultyId:" + difficultyId);
        return instance;
    }

    public static WorldMapInstance GetNextAvailableInstance(int worldId, int ownerId, byte difficult, int maxPlayers, bool autoDestroy)
    {
        return GetNextAvailableInstance(worldId, ownerId, difficult, null, maxPlayers, autoDestroy);
    }

    public static WorldMapInstance GetNextAvailableInstance(int worldId, Player player)
    {
        int maxPlayers = DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(worldId, player.GetRace());
        WorldMapInstance instance = GetNextAvailableInstance(worldId, 0, (byte)0, null, maxPlayers, true);
        instance.Register(player.GetObjectId());
        return instance;
    }

    public static WorldMapInstance GetNextAvailableInstance(int worldId, byte difficult, int maxPlayers)
    {
        return GetNextAvailableInstance(worldId, 0, difficult, null, maxPlayers, true);
    }

    /// <summary>Instance will be destroyed. All players moved to bind location. All objects deleted.</summary>
    public static void DestroyInstance(WorldMapInstance instance)
    {
        if (instance.GetEmptyInstanceTask() != null)
            instance.GetEmptyInstanceTask().Cancel(false);

        int worldId = instance.GetMapId();
        WorldMap map = World.World.GetInstance().GetWorldMap(worldId);
        if (!map.IsInstanceType())
            return;
        int instanceId = instance.GetInstanceId();

        map.RemoveWorldMapInstance(instanceId);

        log.LogInformation("Destroying " + instance);

        TemporarySpawnEngine.OnInstanceDestroy(instance); // first unregister all temporary spawns, then despawn mobs
        foreach (VisibleObject obj in instance)
        {
            if (obj is Player player)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_FORCE(0));
                MoveToExitPoint(player);
            }
            else
            {
                obj.GetController().Delete();
            }
        }
        instance.GetInstanceHandler().OnInstanceDestroy();
        WalkerFormator.OnInstanceDestroy(worldId, instanceId);
    }

    public static WorldMapInstance GetOrRegisterInstance(int worldId, Player player)
    {
        WorldMapInstance instance = GetRegisteredInstance(worldId, player.GetObjectId());
        if (instance == null)
            instance = GetNextAvailableInstance(worldId, player);
        return instance;
    }

    public static WorldMapInstance GetRegisteredInstance(int worldId, int objectId)
    {
        foreach (WorldMapInstance instance in World.World.GetInstance().GetWorldMap(worldId))
        {
            if (instance.IsRegistered(objectId))
                return instance;
        }
        return null;
    }

    /// <summary>Instance for the given house or studio.</summary>
    public static WorldMapInstance GetOrCreateHouseInstance(House house)
    {
        WorldMapInstance instance = house.GetPosition() == null ? null : house.GetPosition().GetWorldMapInstance();
        if (instance == null && house.GetBuilding().GetType_() == BuildingType.PERSONAL_INS)
        { // studio
            instance = GetOrCreatePersonalInstance(house.GetAddress().GetMapId(), house.GetOwnerId());
        }
        if (instance == null) // should never happen since only studios are spawned on demand
            throw new NullReferenceException(house + " has no instance");
        return instance;
    }

    private static WorldMapInstance GetOrCreatePersonalInstance(int worldId, int ownerId)
    {
        if (ownerId == 0 || !WorldMapTypeExtensions.GetWorld(worldId).IsPersonal())
            return null;

        foreach (WorldMapInstance instance in World.World.GetInstance().GetWorldMap(worldId))
        {
            if (instance.IsPersonal() && instance.GetOwnerId() == ownerId)
                return instance;
        }
        return GetNextAvailableInstance(worldId, ownerId, (byte)0, 0, true);
    }

    public static void OnPlayerLogin(Player player)
    {
        int worldId = player.GetWorldId();
        int ownerId = player.GetCommonData().GetWorldOwnerId();
        WorldMapInstance instance = ownerId != 0 ? GetOrCreatePersonalInstance(worldId, ownerId) : GetRegisteredInstance(worldId, player.GetObjectId());
        if (instance == null && player.GetWorldMapInstance().GetTemplate().IsInstance() || instance != null && instance.IsFull())
            MoveToExitPoint(player);
        else if (instance != null) // set to correct instanceId (default on login is 1)
            World.World.GetInstance().SetPosition(player, worldId, instance.GetInstanceId(), player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());
        player.GetWorldMapInstance().GetInstanceHandler().OnPlayerLogin(player);
    }

    public static void MoveToExitPoint(Player player)
    {
        TeleportService.MoveToInstanceExit(player, player.GetWorldId(), player.GetRace());
    }

    public static bool InstanceExists(int worldId, int instanceId)
    {
        return World.World.GetInstance().GetWorldMap(worldId).GetWorldMapInstance(instanceId) != null;
    }

    private class EmptyInstanceCheckerTask
    {
        private readonly WorldMapInstance worldMapInstance;
        private readonly long taskStartTime;

        public EmptyInstanceCheckerTask(WorldMapInstance worldMapInstance)
        {
            this.worldMapInstance = worldMapInstance;
            this.taskStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private bool CanDestroyInstance()
        {
            if (worldMapInstance.GetPlayersInside().Count != 0)
                return false;
            return worldMapInstance.IsPersonal() || IsRegisteredTeamDisbanded() || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > CalculateDestroyTime() - 1000;
        }

        private bool IsRegisteredTeamDisbanded()
        {
            GeneralTeam registeredTeam = worldMapInstance.GetRegisteredTeam();
            return registeredTeam != null && registeredTeam.IsDisbanded();
        }

        private long CalculateDestroyTime()
        {
            long lastActivity = Math.Max(taskStartTime, worldMapInstance.GetLastPlayerLeaveTime());
            return lastActivity + GetDestroyDelaySeconds(worldMapInstance) * 1000;
        }

        public void Run()
        {
            if (CanDestroyInstance())
                DestroyInstance(worldMapInstance);
        }
    }

    public static void OnLogout(Player player)
    {
        player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnPlayerLogout(player);
    }

    public static void OnEnterInstance(Player player)
    {
        player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnEnterInstance(player);
        AutoGroupService.GetInstance().OnEnterInstance(player);
    }

    public static void OnLeaveInstance(Player player)
    {
        WorldMapInstance instance = player.GetWorldMapInstance();
        instance.GetInstanceHandler().OnLeaveInstance(player);
        if (instance.GetRegisteredCount() > 0)
        {
            if (instance.GetMaxPlayers() == 1) // solo instance
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE(GetDestroyDelaySeconds(instance) / 60));
            else if (instance.GetRegisteredTeam() != null && instance.GetRegisteredTeam().GetMembers().Count == 0)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_PARTY(0));
            else if (instance.GetPlayersInside().Count <= 1)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_PARTY(GetDestroyDelaySeconds(instance) / 60));
        }

        if (AutoGroupConfig.AUTO_GROUP_ENABLE)
            AutoGroupService.GetInstance().OnLeaveInstance(player);
    }

    public static void OnEnterZone(Player player, ZoneInstance zone)
    {
        player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnEnterZone(player, zone);
    }

    public static void OnLeaveZone(Player player, ZoneInstance zone)
    {
        player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnLeaveZone(player, zone);
    }

    public static int GetInstanceRate(Player player, int mapId)
    {
        return player.HasPermission(MembershipConfig.INSTANCES_COOLDOWN) && !InstanceConfig.INSTANCE_COOLDOWN_RATE_EXCLUDED_MAPS.Contains(mapId) ? InstanceConfig.INSTANCE_COOLDOWN_RATE : 1;
    }

    public static int GetDestroyDelaySeconds(WorldMapInstance worldMapInstance)
    {
        return worldMapInstance.GetMaxPlayers() == 1 ? InstanceConfig.SOLO_INSTANCE_DESTROY_DELAY_SECONDS : InstanceConfig.INSTANCE_DESTROY_DELAY_SECONDS;
    }
}
