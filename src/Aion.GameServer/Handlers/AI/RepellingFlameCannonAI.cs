using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/idgelDome/RepellingFlameCannonAI (@author Ritsu, Estrayl).</summary>
[AIName("repelling_flame_cannon")]
public class RepellingFlameCannonAI : NpcAI
{
    private ScheduledTask? skillTask;

    public RepellingFlameCannonAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, 21648);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(1000), System.TimeSpan.FromMilliseconds(1000));
    }

    protected override void HandleDespawned()
    {
        skillTask!.Cancel(true);
        base.HandleDespawned();
    }
}
