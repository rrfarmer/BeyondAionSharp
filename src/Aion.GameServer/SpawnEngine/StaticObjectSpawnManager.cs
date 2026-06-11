using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/StaticObjectSpawnManager (ATracer). Straight transcription. StaticObject/StaticObjectController/World/DataManager/SpawnGroup red-tolerated.</summary>
public class StaticObjectSpawnManager
{
    public static void SpawnTemplate(SpawnGroup spawn, int instanceIndex)
    {
        VisibleObjectTemplate objectTemplate = DataManager.ITEM_DATA.GetItemTemplate(spawn.GetNpcId());
        if (objectTemplate == null)
            return;

        if (spawn.HasPool())
        {
            spawn.ResetPoolSpots(instanceIndex);
            for (int i = 0; i < spawn.GetPool(); i++)
            {
                SpawnTemplate template = spawn.ReserveRandomFreePoolSpot(instanceIndex);
                StaticObject staticObject = new StaticObject(new StaticObjectController(), template, objectTemplate);
                staticObject.SetKnownlist(new PlayerAwareKnownList(staticObject));
                BringIntoWorld(staticObject, template, instanceIndex);
            }
        }
        else
        {
            foreach (SpawnTemplate template in spawn.GetSpawnTemplates())
            {
                StaticObject staticObject = new StaticObject(new StaticObjectController(), template, objectTemplate);
                staticObject.SetKnownlist(new PlayerAwareKnownList(staticObject));
                BringIntoWorld(staticObject, template, instanceIndex);
            }
        }
    }

    private static void BringIntoWorld(VisibleObject visibleObject, SpawnTemplate spawn, int instanceIndex)
    {
        World world = World.GetInstance();
        world.StoreObject(visibleObject);
        world.SetPosition(visibleObject, spawn.GetWorldId(), instanceIndex, spawn.GetX(), spawn.GetY(), spawn.GetZ(), spawn.GetHeading());
        world.Spawn(visibleObject);
    }
}
