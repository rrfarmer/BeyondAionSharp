using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("orissans_summon")]
public class OrissansSummonAI : GeneralNpcAI
{
    private readonly AtomicBoolean isActive = new AtomicBoolean();

    public OrissansSummonAI(Npc owner) : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        if (attacker != GetOwner() && attacker is Npc)
        {
            if (isActive.CompareAndSet(false, true))
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    if (GetNpcId() == 855699)
                    {
                        AIActions.TargetSelf(this);
                        AIActions.UseSkill(this, 21638, 67);
                    }
                    else
                    {
                        AIActions.TargetCreature(this, attacker);
                        AIActions.UseSkill(this, 21639, 13);
                    }
                    return ValueTask.CompletedTask;
                }, 1000L);
            }
            return 10000;
        }
        return damage;
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        GetOwner().GetController().Die();
    }

    protected override void HandleBackHome()
    {
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
