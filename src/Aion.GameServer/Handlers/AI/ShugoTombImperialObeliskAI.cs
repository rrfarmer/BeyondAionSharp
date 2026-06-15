using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/shugoImperialTomb/ShugoTombImperialObeliskAI (@author Ritsu).</summary>
[AIName("shugo_tomb_imperial_obelisk")]
public class ShugoTombImperialObeliskAI : GeneralNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(70, 35);

    public ShugoTombImperialObeliskAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 70:
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21098, GetOwner(), GetOwner());
                break;
            case 35:
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21099, GetOwner(), GetOwner());
                break;
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21097, GetOwner(), GetOwner());
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }
}
