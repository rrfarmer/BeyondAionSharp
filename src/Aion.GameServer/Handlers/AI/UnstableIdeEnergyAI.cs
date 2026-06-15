using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/idgelDome/UnstableIdeEnergyAI (@author Ritsu, Estrayl).</summary>
[AIName("unstable_id_energy")]
public class UnstableIdeEnergyAI : NpcAI
{
    private ScheduledTask skillTask;

    public UnstableIdeEnergyAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleSkill(Rnd.Get(10000, 30000));
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    private void ScheduleSkill(int delay)
    {
        skillTask = ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill(this, 21559), delay);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21559:
                ScheduleSkill(Rnd.Get(10000, 30000));
                break;
        }
    }

    protected override void HandleDespawned()
    {
        skillTask.Cancel(true);
        base.HandleDespawned();
    }
}
