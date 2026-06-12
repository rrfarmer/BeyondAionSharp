using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;
using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SummonServantEffect (ATracer) : SummonEffect. INITIAL_SPAWN_DELAY=3000 const; Math.toRadians→*PI/180; getClosestCollision; spawnServant protected (overridden by SkillArea); IllegalArgumentException→ArgumentException; Future→ScheduledTask schedule(lambda, spawnDuration*1000L+INITIAL_SPAWN_DELAY). Servant/NpcObjectType/FirstTargetAttribute red-tolerated.</summary>
[XmlType("SummonServantEffect")]
public class SummonServantEffect : SummonEffect
{
    private const int INITIAL_SPAWN_DELAY = 3000; // Seems to be around 2.5s

    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(effect.GetEffector().GetHeading());
        float x = effector.GetX() + (float)(Math.Cos(radian) * 2);
        float y = effector.GetY() + (float)(Math.Sin(radian) * 2);
        Vector3f pos = GeoService.GetInstance().GetClosestCollision(effector, x, y, effector.GetZ(), true, CollisionIntention.DEFAULT_COLLISIONS.GetId(),
            IgnoreProperties.Of(effector.GetRace()));
        Servant servant = SpawnServant(effect, time, NpcObjectType.SERVANT, pos.GetX(), pos.GetY(), pos.GetZ());
        servant.GetAi().OnCreatureEvent(AiEventType.ATTACK, effect.GetEffected());
    }

    protected Servant SpawnServant(Effect effect, int spawnDuration, NpcObjectType npcObjectType, float x, float y, float z)
    {
        Creature effector = effect.GetEffector();
        if (effect.GetEffected() == null && effect.GetSkillTemplate().GetProperties().GetFirstTarget() != FirstTargetAttribute.POINT)
            throw new ArgumentException("Servant " + npcId + "cannot be spawned by " + effector + " (target: null)");

        SpawnTemplate spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(effector.GetWorldId(), npcId, x, y, z, effector.GetHeading());
        Servant servant = VisibleObjectSpawner.SpawnServant(spawn, effector.GetInstanceId(), effector, effect.GetSkillLevel(), npcObjectType);

        ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct => { servant.GetController().Delete(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(spawnDuration * 1000L + INITIAL_SPAWN_DELAY));
        servant.GetController().AddTask(TaskId.DESPAWN, task);
        return servant;
    }
}
