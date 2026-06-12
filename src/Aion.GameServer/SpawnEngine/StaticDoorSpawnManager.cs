using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Staticdoor;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/StaticDoorSpawnManager (MrPoke). slf4j logger→ILogger; log.info→LogInformation. SpawnEngine/StaticObjectController/StaticDoor/GeoService/DataManager red-tolerated.</summary>
public class StaticDoorSpawnManager
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(StaticDoorSpawnManager));

    public static void SpawnTemplate(WorldMapInstance instance)
    {
        int counter = 0;
        foreach (StaticDoorTemplate data in DataManager.STATICDOOR_DATA.GetStaticDoors(instance.GetMapId()))
        {
            SpawnTemplate spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(instance.GetMapId(), 300001, data.GetX(), data.GetY(), data.GetZ(), (byte)0);
            spawn.SetStaticId(data.GetId());
            StaticDoor staticDoor = new StaticDoor(new StaticObjectController(), spawn, data, instance.GetInstanceId());
            staticDoor.SetKnownlist(new PlayerAwareKnownList(staticDoor));
            Aion.GameServer.SpawnEngine.SpawnEngine.BringIntoWorld(staticDoor, spawn, instance.GetInstanceId());
            counter++;
            GeoService.GetInstance().SetDoorState(instance.GetMapId(), instance.GetInstanceId(), data.GetId(), staticDoor.IsOpen());
        }
        if (counter > 0)
            Log.LogInformation("Spawned " + counter + " static doors in " + instance);
    }
}
