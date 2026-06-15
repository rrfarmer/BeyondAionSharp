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
    private readonly HpPhases hpPhases = new HpPhases(50, 25, 10);

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
            case 50:
                UseSelfBuff(20938);
                break;
            case 25:
                UseSelfBuff(20939);
                break;
            case 10:
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
