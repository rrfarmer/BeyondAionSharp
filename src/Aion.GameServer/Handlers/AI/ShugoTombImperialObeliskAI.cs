using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/shugoImperialTomb/ShugoTombImperialObeliskAI (@author Ritsu), with the HP
/// rungs and their trigger events taken from retail instead -- see docs/retail-ai-fidelity.md.
/// </summary>
[AIName("shugo_tomb_imperial_obelisk")]
public class ShugoTombImperialObeliskAI : GeneralNpcAI, HpPhases.PhaseHandler
{
    /// <summary>Retail's first rung: is_hp_in_boundary(larger_than=30, less_than=69), exclusive both ends.</summary>
    public const int FirstRungCeilingPercent = 68;
    public const int FirstRungFloorPercent = 30;

    /// <summary>Retail's second rung: is_hp_in_boundary(larger_than=0, less_than=29).</summary>
    public const int SecondRungCeilingPercent = 28;

    private readonly HpPhases hpPhases = new HpPhases(FirstRungCeilingPercent, SecondRungCeilingPercent);

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
        AdvanceRungs();
    }

    /// <summary>
    /// Retail evaluates both rungs on a single on_attacked -- priority 6 then priority 5 -- so a hit that
    /// crosses both plays what it should immediately. TryEnterNextPhase advances one rung per call, which
    /// would defer the second to the following hit, so it is drained here instead.
    /// </summary>
    private void AdvanceRungs()
    {
        int before;
        do
        {
            before = hpPhases.GetCurrentPhase();
            hpPhases.TryEnterNextPhase(this);
        }
        while (hpPhases.GetCurrentPhase() != before);
    }

    /// <summary>
    /// Retail hangs both rungs on on_attacked AND on_spelled. Damage that carries an Effect already reaches
    /// HandleAttack through the aggro path; a spell that deals no damage adds no hate and reaches neither --
    /// and it does not reach the Spelled event either, because that is raised from the damage path and a
    /// damageless skill never enters it. This hook is the one that fires for every skill that lands.
    /// </summary>
    public override void OnEffectApplied(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        base.OnEffectApplied(effect);
        AdvanceRungs();
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case FirstRungCeilingPercent:
                // Retail's boundary is exclusive at the bottom too, so a rung the obelisk outran in a single
                // hit is never played. HpPhases has already consumed it by the time we get here; dropping it
                // is therefore exactly retail's behaviour, where HpPhases alone would play it late instead.
                if (GetLifeStats().GetHpPercentage() > FirstRungFloorPercent)
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21098, GetOwner(), GetOwner());
                break;
            case SecondRungCeilingPercent:
                // The matching larger_than=0 needs no check: HpPhases will not fire on a dead owner.
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
