using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/SacrificialSoulAI (@author Luzien).</summary>
[AIName("templeSoul")]
public class SacrificialSoulAI : GeneralNpcAI
{
    private Npc boss;

    public SacrificialSoulAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AIActions.UseSkill(this, 18901);
        this.SetStateIfNot(AIState.FOLLOWING);
        boss = GetPosition().GetWorldMapInstance().GetNpc(216263);
        if (boss != null && !boss.IsDead())
        {
            AIActions.TargetCreature(this, boss);
            GetMoveController().MoveToTargetObject();
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (creature.GetEffectController().HasAbnormalEffect(18959))
        {
            GetMoveController().AbortMove();
            AIActions.DeleteOwner(this);
        }
    }

    protected override void HandleMoveArrived()
    {
        if (boss != null && !boss.IsDead())
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18960, 55, boss).UseNoAnimationSkill();
            AIActions.DeleteOwner(this);
        }
    }

    public override bool CanThink()
    {
        return false;
    }
}
