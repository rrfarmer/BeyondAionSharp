using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionSnareTurretAI (@author Estrayl).</summary>
[AIName("eternal_bastion_snare_turret")]
public class EternalBastionSnareTurretAI : EternalBastionAggressiveNpcAI
{
    private readonly AtomicBoolean isSkillDisabled = new AtomicBoolean();

    public EternalBastionSnareTurretAI(Npc owner)
        : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }

    protected override void HandleFinishAttack()
    {
        EmoteManager.EmoteStopAttacking(GetOwner());
        GetOwner().GetController().LoseAggro(false);
        GetOwner().SetSkillNumber(0);
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (isSkillDisabled.CompareAndSet(false, true))
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21116, 65, creature).UseNoAnimationSkill(); // Magnetic Pull
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21116) // Magnetic Pull
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21117, 65, GetOwner()).UseNoAnimationSkill(); // Blanket Venting
        else if (skillTemplate.GetSkillId() == 21117) // Blanket Venting
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21117, GetOwner(), GetOwner()); // Work-around since HEROs ignore abnormal states atm
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect != null && effect.GetSkillId() == 21117)
            isSkillDisabled.Set(false);
    }
}
