using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Raging kraterr (211715) and its summoned twin (280332). Retail pattern <c>ND2_ElementalSu</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The fire half of
/// <see cref="ElementalSummonerPattern"/>: the same fight as <see cref="FrostmaneLestinAI"/>, down to
/// every delay and count, calling <b>faithful servants</b> (280333, 280334, 280335) where Lestin calls
/// subordinates. It is the sender <see cref="ElementalWaveAI"/> shipped without.
/// <para>
/// <b>He was on <c>summoner</c>, and that table was an observation.</b> Set against the pattern it
/// gets five things wrong, which is worth listing because it is what an approximated summon table
/// looks like:
/// </para>
/// <list type="table">
/// <item><term>thresholds</term><description>90 / 70 / 40 against retail's bands 66–90, 41–65,
/// 21–40</description></item>
/// <item><term>which add</term><description><b>280333 all three times</b>, where retail calls a
/// different elemental per wave</description></item>
/// <item><term>how many</term><description>two to five at random, against exactly four</description></item>
/// <item><term>how far</term><description>ten metres, against twelve then fifteen</description></item>
/// <item><term>how long</term><description>no lifetime and no hand-over, against ten minutes and each
/// wave clearing the one before it</description></item>
/// </list>
/// <para>
/// <b>What moving him off the table costs, stated plainly.</b> The table cast <b>18389</b> alongside
/// each summon and <b>18390</b> at twenty-five percent, and nothing in the pattern runtime replaces
/// them — so this trades two casts for the right waves, lifetimes, hand-over and summon order. Both
/// skills sit at <c>prob="0"</c> in his <c>npc_skills</c> row, meaning they are never randomly chosen
/// and exist only to be driven by something that names them.
/// </para>
/// <para>
/// <b>A tempting index mapping that does not hold.</b> The obvious reading is that 18389 is the
/// pattern's <c>SKILLI_INDEX_0</c> — aionemu casts it beside the summons, retail's three summoning
/// rungs cast index 0, and the <c>prob="0"</c> marks it as index-addressed. The skill data refuses it:
/// 18389 is <b>Fire Wave</b>, a <c>MAGICAL DEBUFF</c> on a <c>DEBUFF</c> slot with a damage-over-time
/// stack, and retail casts index 0 on <c>OBJI_SELF</c>. A debuff is not a self-cast. What aionemu's
/// table records is a cast <em>at the target</em>, which in retail lives on the other timers, not on
/// the summoning rungs. The indices stay unresolved.
/// </para>
/// <para>
/// Worth keeping for whoever does resolve them: the two bosses' skill lists are <b>parallel in
/// shape</b> — 18389/18390 are Fire Wave and Powerful Fire Wave, 18394/18395 are Small and Powerful
/// Bloody Wind, with matching weak/strong stack names — so a mapping established on one is evidence
/// for the other. They share a pattern and not a skill list, so resolving one index does not resolve
/// the other's.
/// </para>
/// </remarks>
[AIName("raging_kraterr")]
public class RagingKraterrAI : PatternAi
{
    // BLF2_NM2_ElementalFireSu1/Su2/Su3_40_An — the faithful servants.
    private const int FirstWave = 280333;
    private const int SecondWave = 280334;
    private const int ThirdWave = 280335;

    private static readonly AiPattern Pattern_ =
        ElementalSummonerPattern.For(FirstWave, SecondWave, ThirdWave);

    public RagingKraterrAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
