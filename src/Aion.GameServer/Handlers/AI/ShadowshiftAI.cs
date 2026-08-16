using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Shadowshift (216247 and 281546), the Catacombs boss. Retail pattern <c>IDCT_Boss_Shadow</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both NPCs were on plain <c>aggressive</c>, and
/// neither spectre it calls up was spawned by anything.
/// <para>
/// The fight is two spectre timers running at once, and they are not a wave on the boss — they are
/// <c>spawn_on_multi_target</c>, <b>one spectre on every player within a hundred metres</b>, each
/// attacking whoever it landed on. Nothing caps the count, so a larger group gets proportionally more
/// of them; that is the mechanic rather than an oversight, and it is why the group's size matters
/// here in a way it does not for a boss that drops a fixed wave at its own feet.
/// </para>
/// <list type="bullet">
/// <item><b>timer 0</b> — a Sum1 spectre on each target three metres out, first at ten seconds and
/// then every twenty-five</item>
/// <item><b>timer 1</b> — a Sum2 spectre on each target ten metres out, first at seven seconds and
/// then <b>every four</b></item>
/// <item><b>timer 2</b> — cast-only, every twenty-eight seconds, not translated</item>
/// <item><b>dying or resetting</b> — the spectres go with it</item>
/// </list>
/// <para>
/// <b>The casts are not translated.</b> Four indices are addressed and both NPCs carry fewer skills
/// than that. Omitted with them: the whole <c>on_attacked</c> and <c>on_spelled</c> surface, which is
/// a four-rung health ladder of self-casts plus a near/far pair, and the timer-2 branch. All of it is
/// casting; the spectres and the timings are index-free and faithful.
/// </para>
/// <para>
/// <b>Also not translated:</b> the <c>control_door</c> on leaving the fight. Our door handling lives
/// in the instance rather than the AI, and nothing in the pattern says which door.
/// </para>
/// </remarks>
[AIName("shadowshift")]
public class ShadowshiftAI : PatternAi
{
    private const int SpectreNear = 281657;
    private const int SpectreFar = 281658;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: dying and resetting both clear exactly this group.</summary>
    private const int Spectres = 1;

    /// <summary>Retail's <c>valid_distance</c>: a hundred metres, which is the whole room.</summary>
    private const float Reach = 100f;

    /// <summary>
    /// Retail sets no <c>total_set_to_spawn</c>, so every valid target gets one. The runtime wants a
    /// number, and this is high enough to be no cap for any group that can enter a Catacombs run.
    /// </summary>
    private const int NoCap = 64;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(9, "", When.Always,
                Do.ArmTimer(0, 10000),
                Do.ArmTimer(1, 7000),
                Do.ArmTimer(2, 28000))),

        OnBattleTimer = Of(
            Branch(8, "", [When.Timer(0)],
                Do.ArmTimer(0, 25000),
                Do.SpawnOnEachTarget(SpectreNear, Spectres, Reach, NoCap, MultiTargetOrder.Random, range: 3f)),

            Branch(7, "", [When.Timer(1)],
                Do.ArmTimer(1, 4000),
                Do.SpawnOnEachTarget(SpectreFar, Spectres, Reach, NoCap, MultiTargetOrder.Random, range: 10f)),

            // Timer 2 is cast-only, kept so the chain still re-arms as retail's does.
            Branch(6, "", [When.Timer(2)],
                Do.ArmTimer(2, 28000))),

        OnDie = Of(
            Branch(10, "", When.Always,
                Do.Despawn(Spectres))),

        OnLeaveAttack = Of(
            Branch(10, "", When.Always,
                Do.Despawn(Spectres))),
    };

    public ShadowshiftAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
