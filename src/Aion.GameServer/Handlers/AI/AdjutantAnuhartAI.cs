using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller, Estrayl
/// </summary>
[AIName("adjutantanuhart")]
public class AdjutantAnuhartAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail thresholds, from pattern IDTiamat_Anuhart: three one-shot latched steps, each
    /// casting the next of his escalating self-buffs. The 50/25/10 these replace were derived
    /// from watching the fight. See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(70, 40, 22);

    public AdjutantAnuhartAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 20747) // Blade Storm
        {
            SkillEngine.SkillEngine.GetInstance().ApplyEffect(20749, GetOwner(), GetOwner());
            GetEffectController().SetAbnormal(AbnormalState.SANCTUARY);
            Spawn(283099, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 20747) // Blade Storm
            GetEffectController().UnsetAbnormal(AbnormalState.SANCTUARY);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 70:
                UseSelfBuff(20938);
                break;
            case 40:
                UseSelfBuff(20939);
                break;
            case 22:
                UseSelfBuff(20940);
                break;
        }
    }

    private void UseSelfBuff(int buffSkillId)
    {
        AIActions.TargetSelf(this);
        AIActions.UseSkill(this, buffSkillId);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
