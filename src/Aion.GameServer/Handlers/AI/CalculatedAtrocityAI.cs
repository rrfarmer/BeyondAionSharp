using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/CalculatedAtrocityAI (Estrayl).</summary>
[AIName("calculated_atrocity")]
public class CalculatedAtrocityAI : GeneralNpcAI
{
    private ScheduledTask task;

    public CalculatedAtrocityAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * 0.7f;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            CalculateAndApplyDamage();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(500), System.TimeSpan.FromMilliseconds(2000));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(11000));
    }

    private void CalculateAndApplyDamage()
    {
        GetKnownList().ForEachPlayer(p =>
        {
            if (GetOwner().CanSee(p) && PositionUtil.IsInRange(GetOwner(), p, 45) && PositionUtil.IsInFrontOf(p, GetOwner(), 45))
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21894, GetOwner(), p);
        });
    }

    protected override void HandleDespawned()
    {
        task.Cancel(true);
        base.HandleDespawned();
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
