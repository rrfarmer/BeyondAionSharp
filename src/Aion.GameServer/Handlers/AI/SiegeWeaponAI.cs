using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Services.Summons;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("siege_weapon")]
public class SiegeWeaponAI : AITemplate<Summon>
{
    private long lastAttackTime;
    private int skill;
    private int skillLvl;
    private int duration;

    public SiegeWeaponAI(Summon owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        this.SetStateIfNot(AIState.IDLE);
        SummonsService.DoMode(SummonMode.GUARD, GetOwner());
        NpcSkillTemplate skillTemplate = GetNpcSkillTemplates().GetNpcSkills()[0];
        skill = skillTemplate.GetSkillId();
        skillLvl = skillTemplate.GetSkillLevel();
        duration = DataManager.SKILL_DATA.GetSkillTemplate(skill).GetDuration();
    }

    protected override void HandleFollowMe(Creature creature)
    {
        this.SetStateIfNot(AIState.FOLLOWING);
    }

    protected override void HandleStopFollowMe(Creature creature)
    {
        this.SetStateIfNot(AIState.IDLE);
        this.GetOwner().GetMoveController().AbortMove();
    }

    protected override void HandleTargetTooFar()
    {
        GetOwner().GetMoveController().MoveToDestination();
    }

    protected override void HandleMoveArrived()
    {
        this.GetOwner().GetController().OnMove();
        this.GetOwner().GetMoveController().AbortMove();
    }

    protected override void HandleMoveValidate()
    {
        GetOwner().GetController().OnMove();
        GetOwner().GetMoveController().MoveToTargetObject();
    }

    private NpcSkillTemplates GetNpcSkillTemplates()
    {
        return ((SiegeWeaponController)GetOwner().GetController()).GetNpcSkillTemplates();
    }

    protected override void HandleAttack(Creature creature)
    {
        if (creature == null || GetOwner().GetMode() != SummonMode.ATTACK)
            return;
        if (GetOwner().GetController() is SiegeWeaponController swc && swc.IsValidTarget(creature))
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastAttackTime > duration + 2000)
            {
                lastAttackTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                GetOwner().GetController().UseSkill(skill, skillLvl);
            }
        }
    }

    public override bool IsDestinationReached()
    {
        AIState state = GetState();
        if (state == AIState.FOLLOWING)
        {
            return FollowEventHandler.IsInRange(this, GetOwner().GetTarget());
        }
        else
        {
            AILogger.Info(this, "[siege_weapon] calling destinationReached with unhandled state: " + state);
        }
        return true;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }
}
