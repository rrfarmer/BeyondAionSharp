using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Bollvig Blackheart (212314 and 280801), the vampire of Heiron. Retail pattern <c>ND2_WhD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A LEGENDARY boss on plain <c>aggressive</c> with no
/// class at all — the largest thing left on <c>tools/client-extract/audit_missing_ai.py</c> that
/// carries spawns. His whole fight was missing.
/// <list type="table">
/// <item><term>81–100</term><description>casts, and the six-second clock</description></item>
/// <item><term>61–80</term><description>two <b>thirsting bloodwings</b> (280802), fifteen metres out,
/// forty minutes</description></item>
/// <item><term>41–60</term><description>two more, into the same group</description></item>
/// <item><term>21–40</term><description>the bats <b>become vampires</b>, and from then on a
/// <b>cruel vampire</b> (280804) lands on whoever he is fighting every thirty-five seconds</description></item>
/// <item><term>below 20</term><description>the clock stops: no more waves, ever</description></item>
/// </list>
/// <para>
/// <b>The bats are not a wave that grows — they are a wave that changes.</b> Entering 21–40 he
/// broadcasts <c>6187</c>, and every bloodwing still alive sheds itself for a cruel vampire where it
/// stands (see <see cref="BollvigBloodwingAI"/>). So four bats become four vampires in one beat, and
/// the loop that follows adds one more every thirty-five seconds on top.
/// </para>
/// <para>
/// <b>And the loop is bounded by its band at both ends.</b> Timer 5 carries no flag var, so it repeats
/// — but its branch is guarded on 21–40, and the branch below twenty does not re-arm timer 0. Push him
/// under twenty and both the ladder and the vampire loop are over; leave him in the band and they are
/// not. That is the fight's whole shape and it is invisible from the spawn list.
/// </para>
/// <para>
/// <b>On waking he clears up after himself.</b> <c>6630</c> to fifty metres dismisses the relic he
/// leaves on dying — see <see cref="BollvigRelicAI"/> — so a second pull does not find the first
/// kill's reward still standing.
/// </para>
/// <para>
/// <b>Not translated.</b> Ten skill indices across timers 1, 2, 3, 4, 6 and 10 and the branches that
/// carry them; the <c>is_user_flying</c> guard on timer 10, for which we have no vocabulary; and
/// broadcasts <c>6185</c> and <c>6188</c>, whose only listeners answer with a cast. The 81–100 rung is
/// dropped for the usual reason: its re-arm is the same six seconds the fallback below it already
/// gives, so it changes nothing we can express.
/// </para>
/// </remarks>
[AIName("bollvig_blackheart")]
public class BollvigBlackheartAI : PatternAi
{
    /// <summary><c>BLF3_NM_VampireSumBat_50_An</c> — thirsting bloodwing.</summary>
    private const int Bloodwing = 280802;

    /// <summary><c>BLF3_NM_VampireSumVam_50_Ae</c> — cruel vampire.</summary>
    private const int CruelVampire = 280804;

    /// <summary>Retail's <c>SPAWN_ID_1</c> and <c>SPAWN_ID_2</c>: the bats and the vampires.</summary>
    private const int Bats = 1;
    private const int Vampires = 2;

    private const int PerWave = 2;
    private const float BatRing = 15f;

    /// <summary>Forty minutes on the bats; the vampires are given the same by their own branch.</summary>
    private const int BatLife = 2400;

    /// <summary>
    /// Retail's <c>live_time</c> on the vampire that lands on his target — six and three-quarter
    /// hours, which is its way of saying "until the fight ends". The despawn on leaving or dying is
    /// what actually removes them.
    /// </summary>
    private const int VampireLife = 24000;

    private const float VampireReach = 50f;

    // Retail's ALPHA_2..5. ALPHA_1 belongs to the 81-100 rung, which is not translated.
    private const int Below80 = 2;
    private const int Below60 = 3;
    private const int Below40 = 4;
    private const int Below20 = 5;

    private const int HeartbeatMillis = 6000;

    /// <summary>What he leaves behind, and where. Retail places it absolutely.</summary>
    private const int Relic = 204655;
    private static readonly SpawnSpot RelicMark = new SpawnSpot(1001f, 2828f, 235.66f);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 10 here, which is a cast loop behind an is_user_flying guard.
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(BollvigRelicAI.ClearTheRelic, BollvigRelicAI.Reach))),

        OnEnterAttack = Of(
            Branch(15, "", When.Always,
                Do.ArmTimer(0, 20000))),

        OnBattleTimer = Of(
            // Arms timer 6 and not timer 0, so the ladder ends here -- and with it the vampire loop,
            // whose own branch is guarded on 21-40.
            Branch(12, "below 20", [When.Timer(0), When.HpBelow(20), When.FirstTime(Below20)],
                Do.ArmTimer(6, 20000)),

            Branch(11, "21-40 vampire", [When.Timer(5), When.HpBetween(21, 40)],
                Do.ArmTimer(5, 35000),
                Do.SpawnOnTarget(CruelVampire, Vampires, count: 1, liveSeconds: VampireLife)),

            Branch(10, "21-40 opens", [When.Timer(0), When.HpBetween(21, 40), When.FirstTime(Below40)],
                Do.ArmTimer(5, 17000),
                Do.ArmTimer(0, 15000),
                Do.Broadcast(BollvigBloodwingAI.ShedYourWings, VampireReach)),

            Branch(8, "41-60", [When.Timer(0), When.HpBetween(41, 60), When.FirstTime(Below60)],
                Do.ArmTimer(0, 8000),
                Do.SpawnNear(Bloodwing, Bats, count: PerWave, range: BatRing, liveSeconds: BatLife)),

            Branch(5, "61-80", [When.Timer(0), When.HpBetween(61, 80), When.FirstTime(Below80)],
                Do.ArmTimer(0, 8000),
                Do.SpawnNear(Bloodwing, Bats, count: PerWave, range: BatRing, liveSeconds: BatLife)),

            Branch(2, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),

        OnLeaveAttack = Of(
            Branch(13, "", When.Always,
                Do.Despawn(Bats), Do.Despawn(Vampires))),

        OnDie = Of(
            Branch(16, "", When.Always,
                Do.Despawn(Bats), Do.Despawn(Vampires),
                Do.SpawnAt(Relic, 0, 0, RelicMark))),
    };

    public BollvigBlackheartAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The thirsting bloodwings Bollvig calls (280802). Retail pattern <c>ND2_Sum_WhD1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>A bat does not die when he reaches the middle of the fight — it changes.</b> On <c>6187</c> it
/// spawns a <b>cruel vampire</b> (280804) where it stands, with the same forty minutes, and removes
/// itself. Every bloodwing still alive turns over in one beat, which is what makes the two earlier
/// waves matter later rather than only when they land.
/// </para>
/// <para>
/// <b>And killing one is not free.</b> A bloodwing brought down by a player leaves a <b>vicious
/// bloodwing</b> (280803) for fifteen seconds — retail's <c>on_killed_by_user</c>. Our runtime raises
/// one death event, so it fires however the bat died; nothing but a player is fighting them.
/// </para>
/// <para>
/// <b>Not translated:</b> the handler for <c>6188</c>, which is a single cast.
/// </para>
/// </remarks>
[AIName("bollvig_bloodwing")]
public class BollvigBloodwingAI : PatternAi
{
    /// <summary>Retail's message: every bat still standing becomes a vampire.</summary>
    public const int ShedYourWings = 6187;

    private const int CruelVampire = 280804;
    private const int ViciousBloodwing = 280803;

    private const int Grown = 1;
    private const int Left = 2;

    private const int VampireLife = 2400;
    private const int ViciousLife = 15;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(2, "", [When.Message(ShedYourWings)],
                Do.SpawnNear(CruelVampire, Grown, count: 1, liveSeconds: VampireLife),
                Do.DespawnSelf())),

        OnDie = Of(
            Branch(3, "", When.Always,
                Do.SpawnNear(ViciousBloodwing, Left, count: 1, liveSeconds: ViciousLife))),
    };

    public BollvigBloodwingAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Bollvig, the Archon of Storm (204655) — what he leaves where he fell. Retail pattern
/// <c>ND2_WhDSum</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One line: <b>go away when he wakes</b>. It is how a
/// second pull avoids finding the first kill's reward still standing, and it is the other half of the
/// broadcast <see cref="BollvigBlackheartAI"/> sends on waking.
/// <para>
/// It extends <see cref="GeneralNpcAI"/> through the pattern runtime rather than replacing whatever
/// dialogue it carries, because the only branch its pattern has is this one.
/// </para>
/// </remarks>
[AIName("bollvig_relic")]
public class BollvigRelicAI : PatternAi
{
    /// <summary>Retail's message: the previous kill's relic is finished with.</summary>
    public const int ClearTheRelic = 6630;

    /// <summary>Retail's <c>range_as_meter</c>.</summary>
    public const float Reach = 50f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(1, "", [When.Message(ClearTheRelic)],
                Do.DespawnSelf())),
    };

    public BollvigRelicAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
