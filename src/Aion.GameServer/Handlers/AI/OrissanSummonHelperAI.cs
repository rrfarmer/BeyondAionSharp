using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/OrissanSummonHelperAI (@author Estrayl).</summary>
[AIName("orissan_summon_helper")]
public class OrissanSummonHelperAI : GeneralNpcAI
{
    public OrissanSummonHelperAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        HandleSkillTask();
    }

    private void HandleSkillTask()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            AIActions.TargetSelf(this);
            AIActions.UseSkill(this, 21647, 67);
        }, 1000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            WorldPosition p = GetPosition();
            Spawn(GetNpcId() == 855607 ? 855699 : 855700, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
            AIActions.DeleteOwner(this);
        }, 1500L);
    }

    protected override void HandleBackHome()
    {
    }

    public override bool Ask(AIQuestion question)
    {
        if (question == AIQuestion.REWARD_AP_XP_DP_LOOT)
        {
            return false;
        }
        return base.Ask(question);
    }
}
