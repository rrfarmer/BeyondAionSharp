using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author ATracer
/// </summary>
[AIName("servant")]
public class ServantNpcAI : GeneralNpcAI
{
    private ScheduledTask skillTask;

    public ServantNpcAI(Npc owner) : base(owner)
    {
    }

    public override void Think()
    {
        // servants are not thinking
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetCreator() != null)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (GetOwner().GetNpcObjectType() != NpcObjectType.TOTEM)
                    AIActions.TargetCreature(this, (Creature)GetCreator().GetTarget());
                else
                    AIActions.TargetSelf(this);
                HealOrAttack();
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(200));
        }
    }

    private void HealOrAttack()
    {
        NpcSkillEntry skill = GetSkillList().GetRandomSkill();
        if (skill == null)
            return;
        GetOwner().GetGameStats().SetLastSkill(skill);
        int duration = GetOwner().GetNpcObjectType() == NpcObjectType.TOTEM ? 3000 : 5000;
        int startDelay = 1000;
        switch (GetOwner().GetNpcId())
        {
            // Taunting Spirit
            case 833403 or 833404 or 833478 or 833479 or 833480 or 833481:
                duration = 5000;
                break;
            // Battle Banner
            case 833077 or 833078 or 833452 or 833453 or 833454 or 833455:
                duration = 3000;
                startDelay = 100;
                break;
        }
        Creature target = (Creature)GetOwner().GetTarget();
        int durationFinal = duration;
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (target == null || target.IsDead())
            {
                AIActions.DeleteOwner(this);
                CancelTask();
            }
            else
            {
                SkillTemplate template = skill.GetTemplate().GetSkillTemplate();
                if ((template.GetType_() != SkillType.MAGICAL || !GetOwner().GetEffectController().IsAbnormalSet(AbnormalState.SILENCE))
                    && (template.GetType_() != SkillType.PHYSICAL || !GetOwner().GetEffectController().IsAbnormalSet(AbnormalState.BIND))
                    && (!GetOwner().GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
                    && (!GetOwner().IsTransformed() || GetOwner().GetTransformModel().GetBanUseSkills() != 1))
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skill.GetSkillId(), skill.GetSkillLevel(), GetOwner().GetTarget()).UseSkill();
                }
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(startDelay), System.TimeSpan.FromMilliseconds(durationFinal));
        GetOwner().GetController().AddTask(TaskId.SKILL_USE, skillTask);
    }

    public override bool IsMoveSupported()
    {
        return false;
    }

    private void CancelTask()
    {
        if (skillTask != null && !skillTask.IsDone())
            skillTask.Cancel(true);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
