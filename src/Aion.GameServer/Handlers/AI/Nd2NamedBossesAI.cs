using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Exedil (212317). Retail pattern <c>ND2_PhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One of three <c>ND2_*</c> named bosses found by
/// <c>tools/client-extract/audit_missing_ai.py</c> with no AI class at all and adds nobody could
/// reach — the same family as <see cref="FrostmaneLestinAI"/>.
/// <para>
/// <b>His summons are a sequence, not a ladder.</b> Two of the three branches carry no health guard
/// at all; what orders them is priority plus a flag var each, so the first heartbeat fires one, the
/// second fires the next, and each fires once. He calls <b>two</b> ghosts at a time, and they are two
/// different ghosts:
/// </para>
/// <list type="table">
/// <item><term>first tick</term><description>two <c>PrSum2</c> (280775) seven metres out, twenty
/// minutes</description></item>
/// <item><term>next tick</term><description>two <c>PrSum1</c> (280774) six metres out, twenty
/// minutes</description></item>
/// <item><term>below 25, whenever that comes</term><description>two more <c>PrSum2</c>, six metres
/// out, and <b>no lifetime at all</b></description></item>
/// </list>
/// <para>
/// <b>The deep rung ends the chain, and that is retail's own doing.</b> It is the only one of the
/// three that does not re-arm timer 0. So a boss taken below twenty-five percent before his first
/// heartbeat calls those two permanent ghosts and then <em>never summons again</em> — the two
/// twenty-minute pairs are simply skipped. Reproduced rather than tidied: the branch that stops the
/// clock is as much a part of the fight as the ones that keep it running.
/// </para>
/// <para>
/// <b>Not translated.</b> Seven skill indices across timers 2, 4, 5 and 6, all of which these
/// branches arm and none of which carries a spawn.
/// </para>
/// </remarks>
[AIName("exedil")]
public class ExedilAI : PatternAi
{
    private const int GhostPriestOne = 280774;
    private const int GhostPriestTwo = 280775;

    // Retail's SPAWN_ID_1..3, one per step.
    private const int First = 1;
    private const int Second = 2;
    private const int Deep = 3;

    /// <summary>Twenty minutes, which is retail's <c>live_time</c> on the two sequenced pairs.</summary>
    private const int GhostLife = 1200;

    // Retail's ALPHA_2..4.
    private const int Step1 = 2;
    private const int Step2 = 3;
    private const int Below25 = 4;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(12, "", When.Always,
                Do.ArmTimer(0, 10000))),

        OnBattleTimer = Of(
            // Highest priority, and the only branch that does not re-arm the heartbeat.
            Branch(10, "below 25", [When.Timer(0), When.HpBelow(25), When.FirstTime(Below25)],
                Do.SpawnNear(GhostPriestTwo, Deep, count: 2, range: 6f)),

            Branch(7, "first", [When.Timer(0), When.FirstTime(Step2)],
                Do.ArmTimer(0, 8000),
                Do.SpawnNear(GhostPriestTwo, Second, count: 2, range: 7f, liveSeconds: GhostLife)),

            Branch(4, "second", [When.Timer(0), When.FirstTime(Step1)],
                Do.ArmTimer(0, 10000),
                Do.SpawnNear(GhostPriestOne, First, count: 2, range: 6f, liveSeconds: GhostLife))),
    };

    public ExedilAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Ulan (212315). Retail pattern <c>ND2_WhB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The same shape as <see cref="ExedilAI"/> without
/// the health rung: two one-shot steps ordered by priority and a flag each, <b>three</b> ghosts a
/// time rather than two, and both ten metres out.
/// <para>
/// What differs between the two steps is how long they stay: the first pair lasts <b>forty
/// minutes</b> and the second <b>ten</b>. That is the only asymmetry in his summoning, and it is the
/// kind of number a port would flatten without noticing.
/// </para>
/// <para>
/// <b>Not translated:</b> seven skill indices across timers 2, 3, 4, 5 and 6.
/// </para>
/// </remarks>
[AIName("ulan")]
public class UlanAI : PatternAi
{
    private const int GhostWizardOne = 280806;
    private const int GhostWizardTwo = 280807;

    private const int First = 1;
    private const int Second = 2;

    private const int LongLife = 2400;
    private const int ShortLife = 600;

    private const float OutTen = 10f;

    // Retail's ALPHA_2 and ALPHA_3.
    private const int Step1 = 2;
    private const int Step2 = 3;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(12, "", When.Always,
                Do.ArmTimer(0, 12000))),

        OnBattleTimer = Of(
            Branch(7, "first", [When.Timer(0), When.FirstTime(Step2)],
                Do.ArmTimer(0, 7000),
                Do.SpawnNear(GhostWizardTwo, Second, count: 3, range: OutTen, liveSeconds: LongLife)),

            Branch(4, "second", [When.Timer(0), When.FirstTime(Step1)],
                Do.ArmTimer(0, 8000),
                Do.SpawnNear(GhostWizardOne, First, count: 3, range: OutTen, liveSeconds: ShortLife))),
    };

    public UlanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// RM-13b (214800). Retail pattern <c>ND2_AhD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The plainest of the three: one add, one timer, two
/// one-shot steps, and the only thing that changes between them is <b>how many</b>.
/// <list type="bullet">
/// <item>the first heartbeat calls <b>two</b> pretorians;</item>
/// <item>below thirty percent it calls <b>three</b>.</item>
/// </list>
/// <para>
/// Both five metres out and both lasting a minute, which makes them a pressure rather than a wave —
/// and both steps re-arm the heartbeat at five seconds, so the one that has not fired yet is always
/// five seconds away.
/// </para>
/// <para>
/// <b>Not translated:</b> five skill indices, all on timer 1.
/// </para>
/// </remarks>
[AIName("rm13b")]
public class Rm13bAI : PatternAi
{
    /// <summary><c>BIDLF3CL_NM_PretorSumA_45_An</c>.</summary>
    private const int Pretorian = 281278;

    /// <summary>Retail's <c>SPAWN_ID_1</c> — both steps file into the one group.</summary>
    private const int Called = 1;

    private const int PretorianLife = 60;
    private const float OutFive = 5f;

    private const int HeartbeatMillis = 5000;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below30 = 1;
    private const int Opening = 2;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(12, "", When.Always,
                Do.ArmTimer(0, HeartbeatMillis))),

        OnBattleTimer = Of(
            Branch(6, "below 30", [When.Timer(0), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.SpawnNear(Pretorian, Called, count: 3, range: OutFive, liveSeconds: PretorianLife)),

            Branch(5, "opening", [When.Timer(0), When.FirstTime(Opening)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.SpawnNear(Pretorian, Called, count: 2, range: OutFive, liveSeconds: PretorianLife))),
    };

    public Rm13bAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
