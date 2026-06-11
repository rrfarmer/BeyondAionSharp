using System;
using System.Threading.Tasks;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SummonTrapEffect (ATracer) : SummonEffect. Math.toRadians→*PI/180 via PositionUtil; firstTargetSelf→GeoService.getClosestCollision with IgnoreProperties.of(race); newSingleTimeSpawn+spawnTrap; TrapService.registerTrap; schedule(lambda, time*1000L). Trap/Vector3f/CollisionIntention red-tolerated.</summary>
[XmlType("SummonTrapEffect")]
public class SummonTrapEffect : SummonEffect
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        // should only be set if player has no target to avoid errors
        if (effect.GetEffector().GetTarget() == null)
            effect.GetEffector().SetTarget(effect.GetEffector());
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(effect.GetEffector().GetHeading());
        float x = effect.GetX();
        float y = effect.GetY();
        float z = effect.GetZ();
        if (effect.GetSkill().IsFirstTargetSelf())
        {
            Creature effected = effect.GetEffected();
            Vector3f pos = GeoService.GetInstance().GetClosestCollision(effector, effected.GetX() + (float)(Math.Cos(radian) * 2), effected.GetY() + (float)(Math.Sin(radian) * 2), effected.GetZ(), true, CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.Of(effector.GetRace()));
            x = pos.GetX();
            y = pos.GetY();
            z = pos.GetZ();
        }
        byte heading = effector.GetHeading();
        int worldId = effector.GetWorldId();
        int instanceId = effector.GetInstanceId();

        SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(worldId, npcId, x, y, z, heading);
        Trap trap = VisibleObjectSpawner.SpawnTrap(spawn, instanceId, effector);
        TrapService.RegisterTrap(effector.GetObjectId(), trap, true);
        trap.GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(ct => { trap.GetController().Delete(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(time * 1000L)));
    }
}
