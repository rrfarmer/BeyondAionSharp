using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/SummonHomingEffect (ATracer) : SummonEffect. @XmlAttribute(name="npc_count"/"attack_count",required=true)→[XmlAttribute(...)]; per-npc newSingleTimeSpawn+spawnHoming; anonymous ActionObserver(ATTACK)→nested HomingAttackObserver capturing homing; Future→ScheduledTask, schedule(lambda,15*1000); onCreatureEvent(ATTACK). Homing/AIEventType red-tolerated.</summary>
[XmlType("SummonHomingEffect")]
public class SummonHomingEffect : SummonEffect
{
    [XmlAttribute("npc_count")]
    protected int npcCount;
    [XmlAttribute("attack_count")]
    protected int attackCount;

    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        float x = effector.GetX();
        float y = effector.GetY();
        float z = effector.GetZ();
        byte heading = effector.GetHeading();
        int worldId = effector.GetWorldId();
        int instanceId = effector.GetInstanceId();

        for (int i = 0; i < npcCount; i++)
        {
            SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(worldId, npcId, x, y, z, heading);
            Homing homing = VisibleObjectSpawner.SpawnHoming(spawn, instanceId, effector, attackCount, effect.GetSkillId());

            if (attackCount > 0)
            {
                effect.AddObserver(homing, new HomingAttackObserver(homing));
            }
            // Schedule a despawn just in case
            ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct => { homing.GetController().Delete(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(15 * 1000));
            homing.GetController().AddTask(TaskId.DESPAWN, task);
            homing.GetAi().OnCreatureEvent(AIEventType.ATTACK, effect.GetEffected());
        }
    }

    private sealed class HomingAttackObserver : ActionObserver
    {
        private readonly Homing homing;

        public HomingAttackObserver(Homing homing)
            : base(ObserverType.ATTACK)
        {
            this.homing = homing;
        }

        public override void Attack(Creature creature, int skillId)
        {
            homing.SetAttackCount(homing.GetAttackCount() - 1);
            if (homing.GetAttackCount() <= 0)
                homing.GetController().Delete();
        }
    }
}
