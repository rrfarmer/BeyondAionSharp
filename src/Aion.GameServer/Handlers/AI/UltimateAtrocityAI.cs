using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/UltimateAtrocityAI (Luzien, Estrayl).</summary>
[AIName("ultimate_atrocity")]
public class UltimateAtrocityAI : GeneralNpcAI
{
    private ScheduledTask task;

    public UltimateAtrocityAI(Npc owner)
        : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage / 4;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int skill = GetNpcId() switch
        {
            283244 => 21160,
            283240 => 21156,
            283237 or 283241 => 20923,
            _ => 0,
        };

        if (skill == 0)
            return;

        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, skill);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(500), System.TimeSpan.FromMilliseconds(2000));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(11000));
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
