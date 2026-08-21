using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Sematariux's thunder shields, all three sizes (281931, 281932, 281933). Retail patterns
/// <c>LF4_DramataG1</c>, <c>LF4_DramataG2</c> and <c>LF4_DramataG3</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/worlds/inggison/SematariuxThunderShieldAI (Estrayl). Retail-sourced correction
/// below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The shields did not call each other.</b> All three retail patterns carry the same pair: entering
/// attack state broadcasts <b>10010</b> to <b>fifty metres</b> naming whoever pulled them, and
/// <c>on_message 10010</c> answers with <c>add_hate_point 1</c> and <c>attack_most_hating</c>. Six
/// shields stand around Sematariux and each splits twice, so this is the difference between a field
/// that answers a pull and twenty-four objects waiting to be hit one at a time.
/// </para>
/// <para>
/// <b>The call is a call, not a target switch</b>, and that matters at the third tier: retail adds a
/// single hate point and then attacks whoever is <em>then</em> most-hated. On a shield that has just
/// been split off and holds no hate those are the same thing; on one already fighting somebody they
/// are not, and it correctly stays where it is. <see cref="SummonOrder"/> is exactly that pair and is
/// used here rather than a switch.
/// </para>
/// <para>
/// <b>Not translated:</b> each tier's <c>BTIMERI_INDEX_0</c>, armed at 15000 on entering attack state
/// and re-armed at 15000, which casts <c>SKILLI_INDEX_0</c> at its current target. And the corpse npc
/// the smallest tier leaves (<c>BLF4_DramataThunderDeath_57_n</c>, twelve seconds on a path).
/// </para>
/// </remarks>
[AIName("sematariux_thunder_shield")]
public class SematariuxThunderShieldAI : AggressiveNpcAI, INpcMessageListener
{
    /// <summary>Retail's message number for the field, and the reach it is broadcast at.</summary>
    public const int ShieldCall = 10010;
    public const float CallRange = 50f;

    public SematariuxThunderShieldAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail's <c>on_enter_attack_state</c>: tell every shield within fifty metres who pulled us.
    /// </summary>
    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        NpcMessageBus.Broadcast(GetOwner(), ShieldCall, creature, CallRange);
    }

    /// <summary>Retail's <c>on_message</c>: <c>add_hate_point 1</c>, then <c>attack_most_hating</c>.</summary>
    public new void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != ShieldCall || IsDead())
            return;

        SummonOrder.Take(GetOwner(), param);
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(0.1f);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * 0.5f;
    }
}
