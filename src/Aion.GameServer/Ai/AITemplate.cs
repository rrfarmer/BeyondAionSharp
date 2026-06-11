using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Ai;

/// <summary>
/// Java parity: ai/AITemplate&lt;T extends Creature&gt; extends AbstractAI&lt;T&gt; (@author ATracer).
/// Provides default (mostly no-op) implementations of the AbstractAI hooks and AI-interface methods.
/// </summary>
public abstract class AITemplate<T> : AbstractAI<T> where T : Creature
{
    protected AITemplate(T owner) : base(owner)
    {
    }

    public override void Think()
    {
    }

    public override bool CanThink()
    {
        return true;
    }

    public override bool Ask(AIQuestion question)
    {
        return false;
    }

    protected override void HandleActivate()
    {
    }

    protected override void HandleDeactivate()
    {
    }

    protected override void HandleMoveValidate()
    {
    }

    protected override void HandleMoveArrived()
    {
    }

    protected override void HandleAttack(Creature creature)
    {
    }

    protected override bool HandleCreatureNeedsSupport(Creature creature)
    {
        return false;
    }

    protected override bool HandleCreatureNeedsSupportByGuard(Creature creature)
    {
        return false;
    }

    protected override void HandleCreatureSee(Creature creature)
    {
    }

    protected override void HandleCreatureNotSee(Creature creature)
    {
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
    }

    protected override void HandleFollowMe(Creature creature)
    {
    }

    protected override void HandleStopFollowMe(Creature creature)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
    }

    protected override void HandleDialogFinish(Player player)
    {
    }

    protected override void HandleCustomEvent(int eventId, params object[] args)
    {
    }

    protected override void HandleBeforeSpawned()
    {
    }

    protected override void HandleSpawned()
    {
    }

    protected override void HandleDespawned()
    {
    }

    protected override void HandleDied()
    {
    }

    protected override void HandleAttackComplete()
    {
    }

    protected override void HandleFinishAttack()
    {
    }

    protected override void HandleTargetTooFar()
    {
    }

    protected override void HandleTargetGiveup()
    {
    }

    protected override void HandleTargetChanged(Creature creature)
    {
    }

    protected override void HandleNotAtHome()
    {
    }

    protected override void HandleBackHome()
    {
    }

    protected override void HandleDropRegistered()
    {
    }

    public override bool OnPatternShout(Aion.GameServer.Model.Templates.Npcshout.ShoutEventType @event, string pattern, int skillNumber)
    {
        return false;
    }

    protected override void CreatureNeedsHelp(Creature creature)
    {
    }

    public override AttackIntention ChooseAttackIntention()
    {
        return AttackIntention.SIMPLE_ATTACK;
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
    }

    public override void OnEffectApplied(Effect effect)
    {
    }

    public override void OnEffectEnd(Effect effect)
    {
    }

    public override bool IsDestinationReached()
    {
        return false;
    }
}
