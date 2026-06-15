using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/padmarashkasCave/RockSlideAI (@author Ritsu, Estrayl).</summary>
[AIName("rock_slide")]
public class RockSlideAI : AggressiveNpcAI
{
    public RockSlideAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { UseRockSlide(); return ValueTask.CompletedTask; }, 2000L);
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    private void UseRockSlide()
    {
        AIActions.TargetSelf(this);
        AIActions.UseSkill(this, 19295);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        ThreadPoolManager.GetInstance().Schedule(_ => { UseRockSlide(); return ValueTask.CompletedTask; }, 2700L);
    }
}
