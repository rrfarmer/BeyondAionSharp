using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("eternal_bastion_dragon")]
public class EternalBastionDragonAI : EternalBastionAggressiveNpcAI
{
    private double dist;

    public EternalBastionDragonAI(Npc owner) : base(owner)
    {
    }

    public override AttackTypeAnimation GetAttackTypeAnimation(Creature target)
    {
        dist = PositionUtil.GetDistance(GetOwner(), target) - GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide()
            - target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        return dist > 6 ? AttackTypeAnimation.RANGED : AttackTypeAnimation.MELEE;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return effect == null && dist > 6 ? damage * 1.2f : damage;
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return dist > 6 ? ItemAttackType.MAGICAL_EARTH : ItemAttackType.PHYSICAL;
    }

    public override AttackHandAnimation ModifyAttackHandAnimation(AttackHandAnimation attackHandAnimation)
    {
        return Rnd.Get(Enum.GetValues<AttackHandAnimation>());
    }

    public override bool Ask(AIQuestion question)
    {
        if (question == AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES)
            return true;
        return base.Ask(question);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Spawn(284697, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21239) // Gnaw
            GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 50000);
    }
}
