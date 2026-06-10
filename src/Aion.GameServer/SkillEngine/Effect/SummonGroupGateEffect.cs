using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SummonGroupGateEffect (LokiReborn, Neon) : SummonEffect. SpawnEngine.newSingleTimeSpawn; VisibleObjectSpawner.spawnGroupGate; anonymous Runnable→async delegate; Future&lt;?&gt;→ScheduledTask; schedule(...,time*1000)→Schedule(async,TimeSpan.FromMilliseconds); addTask(TaskId.DESPAWN,task). GroupGate/SpawnTemplate red-tolerated.</summary>
[XmlType("SummonGroupGateEffect")]
public class SummonGroupGateEffect : SummonEffect
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        float x = effect.GetX();
        float y = effect.GetY();
        float z = effect.GetZ();
        byte heading = effector.GetHeading();
        int worldId = effector.GetWorldId();
        int instanceId = effector.GetInstanceId();

        SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(worldId, npcId, x, y, z, heading);
        GroupGate groupgate = VisibleObjectSpawner.SpawnGroupGate(spawn, instanceId, effector);

        ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            groupgate.GetController().Delete();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(time * 1000));
        groupgate.GetController().AddTask(TaskId.DESPAWN, task);
    }
}
