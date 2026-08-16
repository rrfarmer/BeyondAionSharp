using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Vanuka Infernus's faithful subordinate (281275). Retail pattern <c>Dragon_G3SlaveSuLizard</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The other half of a call
/// <see cref="VanukaInfernusAI"/> makes every time it summons one of these below 30%: the broadcast
/// carries <b>whoever Vanuka is fighting</b>, and the lizards answer by turning on them.
/// <para>
/// Retail splits the answer on the lizard's own state, and the split is the point. One already
/// fighting <b>switches to a random attacker</b> — it is busy, and the call shakes it loose. One
/// standing idle instead <b>takes the boss's target as its own</b> and goes after them. Same call,
/// opposite effect, depending on whether the lizard has something to do.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Three indices are addressed and the npc has exactly three
/// skills, but nothing forces the mapping: all three carry the same 25% probability and no
/// distinguishing attribute, and their names — Strike, Powerful Knockdown, Tendon Destruction — fit
/// the opener/repeat/pair roles in more than one order. A count match is not corroboration; compare
/// <see cref="NagaSlaveAI"/>, where a BUFF cast on arrival against an ATTACK cast while despawning
/// leaves only one reading. Its two cast-only timers are omitted with them.
/// </para>
/// </remarks>
[AIName("vanuka_lizard")]
public class VanukaLizardAI : PatternAi
{
    /// <summary>Enough to make the boss's quarry its own, matching the other alarm ports.</summary>
    private const int CallHate = 1000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(2, "already fighting", [When.Fighting, When.Message(VanukaInfernusAI.RallyCall)],
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(1, "idle", [When.Idle, When.Message(VanukaInfernusAI.RallyCall)],
                Do.HateMessageTarget(CallHate))),
    };

    public VanukaLizardAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
