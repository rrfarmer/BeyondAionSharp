using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Petrification Incarnate (259614). Retail pattern <c>LDF4b_Tiamat_Lapidification</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He ran <c>aggressive</c>, so the four holy stones his
/// fight is built around never appeared.
/// <para>
/// <b>Found by triaging the AI classes nothing reaches.</b> <c>IncarnateAI</c> names this npc and is
/// bound by no template, no spawn spot and no code — one of nineteen such classes. Most of the nineteen
/// turned out to be superseded by newer classes; this one was not superseded, it was simply dropped, and
/// the npc has been on <c>aggressive</c> ever since.
/// </para>
/// <list type="bullet">
/// <item><b>on engaging</b> — four clocks, at six, eighteen, twenty-four seconds and three minutes</item>
/// <item><b>the eighteen-second clock</b> — <b>four petrification crystals</b> scattered within forty
/// metres of him, each standing thirty-five seconds, then the clock re-arms at thirty</item>
/// </list>
/// <para>
/// <b>The crystals are the same npc Tiamat's incarnations drop</b> (282731), which is why they were
/// already in our data with an AI of their own: this fight is a later use of an add somebody had
/// already ported.
/// </para>
/// <para>
/// <b>Not translated, and it is most of the fight.</b> Fifteen skill indices, including an eight-rung
/// buff ladder on the three-minute clock where every rung is a self-cast behind its own flag var, and
/// the two casts that accompany the crystals. The pattern is seven hundred and fifty lines and nearly
/// all of it is <c>use_skill</c> with an index no client file resolves.
/// <para>
/// Also not translated: the death branch, which spawns <c>LDF4b_FOBJ_Scale</c> at a fixed point for five
/// minutes and broadcasts <c>9999</c>. <b>That devname resolves to no npc in our binding table</b>, so
/// the scale cannot be placed — one of the 12,000 templates still unbound.
/// </para>
/// </para>
/// </remarks>
[AIName("petrification_incarnate")]
public class PetrificationIncarnateAI : PatternAi
{
    /// <summary><c>LDF4b_Tiamat_Crystal_HolyStone</c> — the petrification crystal.</summary>
    private const int PetrificationCrystal = 282731;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: leaving the fight clears what is standing.</summary>
    private const int Crystals = 1;

    /// <summary>Retail's <c>spawn_range</c> and <c>live_time</c> on the crystals.</summary>
    private const float Scatter = 40f;
    private const int CrystalLife = 35;

    private const int PowerClock = 0;
    private const int CrystalClock = 3;
    private const int SweepClock = 4;
    private const int BuffClock = 5;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // All four of retail's clocks are armed, although only one of them has content this port can
        // state. The other three are kept because their delays are the fight's shape: a branch that
        // only casts still decides when the next thing happens, and dropping them would leave the
        // crystal clock looking like the whole encounter.
        OnEnterAttack = Of(
            Branch(14, "SetTimer", When.Always,
                Do.ArmTimer(PowerClock, 6000),
                Do.ArmTimer(CrystalClock, 18000),
                Do.ArmTimer(SweepClock, 24000),
                Do.ArmTimer(BuffClock, 180000))),

        OnBattleTimer = Of(
            Branch(9, "BackAtk & Summon", [When.Timer(CrystalClock)],
                Do.SpawnNear(PetrificationCrystal, Crystals, count: 4, range: Scatter,
                    liveSeconds: CrystalLife),
                Do.ArmTimer(CrystalClock, 30000)),

            Branch(10, "Combo", [When.Timer(SweepClock)],
                Do.ArmTimer(SweepClock, 30000)),

            // Retail's buff ladder: eight rungs, each a self-cast behind its own flag var, all on this
            // clock and all re-arming it at five minutes. None of the casts resolves, so what is left
            // is the clock itself -- kept so it does not stop.
            Branch(8, "Buff ladder", [When.Timer(BuffClock)],
                Do.ArmTimer(BuffClock, 300000)),

            Branch(1, "", [When.Timer(PowerClock)],
                Do.ArmTimer(PowerClock, 6000))),

        // Retail clears the group on leaving the fight, and the crystals have a lifetime besides.
        OnEnterIdle = Of(
            Branch(15, "Dispel_Self", When.Always,
                Do.Despawn(Crystals))),
    };

    public PetrificationIncarnateAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
