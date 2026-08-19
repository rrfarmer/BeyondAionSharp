using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Eternal Bastion's summoner (231128). Retail pattern <c>IDF5_TD_Nor_Pr</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/eternalBastion/EternalBastionSummonerAI (@author Estrayl). Retail-sourced
/// addition below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The summoner never summoned.</b> This class overrode <see cref="Ask"/> and nothing else, so the one
/// thing its name describes did not happen. Retail gives it a single rung, on <c>on_attacked</c> and
/// again on <c>on_spelled</c>: <b>below fifty per cent, once</b>, one <c>revitalizing servant</c>
/// (284441) at its own point within five metres, permanent.
/// </para>
/// <para>
/// <b>The guard is a one-shot, not a threshold that keeps firing.</b> Retail pairs
/// <c>is_hp_lower_than 50</c> with <c>set_flag_var</c>, which is test-and-set, so the servant arrives
/// once however long the fight runs. A rung that re-fired would turn one add into a stream.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>despawn_at_attack_state=TRUE</c> on that spawn. Elsewhere in this port
/// it means "goes when the summoner is pulled", but this spawn happens <i>during</i> combat, so there is
/// no later moment for it to fire on and modelling it would remove the servant immediately.
/// </para>
/// </remarks>
[AIName("eternal_bastion_summoner")]
public class EternalBastionSummonerAI : SummonerAI
{
    /// <summary>Retail's <c>BIDVritra_Base_Boss4_Sum2_65_Ae</c>.</summary>
    public const int RevitalizingServant = 284441;

    /// <summary>Retail's <c>is_hp_lower_than</c> on the rung: strictly below fifty.</summary>
    public const int ServantHpPercent = 50;

    /// <summary>Retail's <c>spawn_range</c>.</summary>
    public const float ServantSpread = 5f;

    private readonly AtomicBoolean servantCalled = new AtomicBoolean(false);

    public EternalBastionSummonerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        CallServant();
    }

    /// <summary>
    /// Retail hangs the same rung on <c>on_spelled</c>. That is this hook rather than the Spelled AI
    /// event, which is raised only from the damage path.
    /// </summary>
    public override void OnEffectApplied(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        base.OnEffectApplied(effect);
        CallServant();
    }

    private void CallServant()
    {
        if (GetLifeStats().GetHpPercentage() >= ServantHpPercent)
            return;
        if (!servantCalled.CompareAndSet(false, true))
            return;
        RndSpawnInRange(RevitalizingServant, ServantSpread);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
