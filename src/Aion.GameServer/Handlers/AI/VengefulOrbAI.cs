using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/danuarReliquary/VengefulOrbAI (@author Ritsu, Estrayl, Yeats).</summary>
[AIName("vengeful_orb")]
public class VengefulOrbAI : NpcAI
{
    private ScheduledTask skillTask;

    public VengefulOrbAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        skillTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!GetOwner().IsDead())
                AIActions.UseSkill(this, 21178);
            return ValueTask.CompletedTask;
        }, 100L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21178)
            GetOwner().GetController().Delete();
    }

    protected override void HandleDespawned()
    {
        if (skillTask != null)
        {
            skillTask.Cancel(false);
        }
        base.HandleDespawned();
    }
}
