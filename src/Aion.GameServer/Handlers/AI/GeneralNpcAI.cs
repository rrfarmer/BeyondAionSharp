using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/GeneralNpcAI (ATracer).</summary>
[AIName("general")]
public class GeneralNpcAI : NpcAI
{
    public GeneralNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override void Think()
    {
        ThinkEventHandler.OnThink(this);
    }

    protected override void HandleAttack(Creature creature)
    {
        AttackEventHandler.OnAttack(this, creature);
    }

    protected override bool HandleCreatureNeedsSupport(Creature creature)
    {
        return AggroEventHandler.OnCreatureNeedsSupport(this, creature);
    }

    protected override void HandleCreatureNotSee(Creature creature)
    {
        if (creature.Equals(GetTarget()))
        {
            GetOwner().GetController().AbortCast();
            OnGeneralEvent(AiEventType.TARGET_TOOFAR);
        }
    }

    protected override void HandleDialogStart(Player player)
    {
        TalkEventHandler.OnTalk(this, player);
    }

    protected override void HandleDialogFinish(Player creature)
    {
        TalkEventHandler.OnFinishTalk(this, creature);
    }

    protected override void HandleFinishAttack()
    {
        AttackEventHandler.OnFinishAttack(this);
    }

    protected override void HandleAttackComplete()
    {
        AttackEventHandler.OnAttackComplete(this);
    }

    protected override void HandleNotAtHome()
    {
        ReturningEventHandler.OnNotAtHome(this);
    }

    protected override void HandleBackHome()
    {
        ReturningEventHandler.OnBackHome(this);
    }

    protected override void HandleTargetTooFar()
    {
        TargetEventHandler.OnTargetTooFar(this);
    }

    protected override void HandleTargetGiveup()
    {
        TargetEventHandler.OnTargetGiveup(this);
    }

    protected override void HandleTargetChanged(Creature creature)
    {
        base.HandleTargetChanged(creature);
        TargetEventHandler.OnTargetChange(this, creature);
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        MoveEventHandler.OnMoveArrived(this);
    }

    public override void HandleCreatureDetected(Creature creature)
    {
        GetOwner().GetPosition().GetWorldMapInstance().GetInstanceHandler().OnCreatureDetected(GetOwner(), creature);
    }

    protected override bool CanHandleEvent(AiEventType eventType)
    {
        switch (eventType)
        {
            case AiEventType.CREATURE_NEEDS_SUPPORT:
                return GetState() == AIState.IDLE || GetState() == AIState.WALKING;
        }
        return base.CanHandleEvent(eventType);
    }

    public override AttackIntention ChooseAttackIntention()
    {
        if (!(GetTarget() is Creature target) || !GetAggroList().IsHating(target))
        {
            Creature mostHated = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
            if (mostHated == null)
                return AttackIntention.FINISH_ATTACK;
            OnCreatureEvent(AiEventType.TARGET_CHANGED, mostHated);
        }

        if (ChooseSkillAttack(GetOwner().GetObjectTemplate().GetAttackRange() == 0))
            return AttackIntention.SKILL_ATTACK;

        return AttackIntention.SIMPLE_ATTACK;
    }

    protected bool ChooseSkillAttack(bool alwaysRandomSkill)
    {
        NpcSkillEntry skill = alwaysRandomSkill ? GetOwner().GetSkillList().GetRandomSkill() : SkillAttackManager.ChooseNextSkill(this);
        if (skill != null)
        {
            GetOwner().GetGameStats().SetLastSkill(skill);
            GetOwner().RemoveNextQueuedSkill(skill);
            return true;
        }
        return false;
    }
}
