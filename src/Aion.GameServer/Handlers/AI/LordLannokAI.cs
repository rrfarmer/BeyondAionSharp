using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Lord Lannok, Adma Stronghold. Retail pattern <c>Adma_DeathknightNamed</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Only the coffin chain is translated: when one of
/// the six suspicious coffins is disturbed it shouts, and he answers by going after whoever disturbed
/// it. Dying, he calls the all-clear, which sends the coffins' skeletons away and lets them shout
/// again. He did neither before — the coffins were inert scenery as far as he was concerned.
/// <para>
/// His rotation is not translated. The pattern addresses seven indices and its branches carry no
/// comments, so nothing corroborates the mapping; his npc_skills probabilities are untouched.
/// </para>
/// <para>
/// <b>The skeleton waves.</b> Once he first drops into 26-50 he lights a thirty-five second fuse, and
/// from then on calls every forty-five seconds: a coin flip sends <b>6605</b> or <b>6607</b> to fifty
/// metres, and coffins D, E and F answer each with a page. See <see cref="SuspiciousCoffinAI"/> for
/// the other half.
/// </para>
/// <para>
/// Retail hangs that fuse off its battle timer 0, whose rotation is not translated here, so nothing
/// would arm it. It is hung off <c>on_attacked</c> instead — a real event rather than an invented
/// cadence — which fires on every swing and so notices the band as soon as he is in it. The one-shot
/// flag makes it a fuse rather than a re-trigger.
/// </para>
/// <para>
/// Also not translated: the three escalating shouts on message 6608, whose sender is not in this
/// encounter's patterns, and the two NPCs his death spawns — an invisible skeleton-despawner and a
/// treasure box, both of which our instance handler already covers or does not need.
/// </para>
/// </remarks>
[AIName("lord_lannok")]
public class LordLannokAI : PatternAi
{
    /// <summary>A coffin calling for help, carrying whoever disturbed it.</summary>
    private const int AlarmMessage = 6609;

    /// <summary>His all-clear on death, which reaches the whole room.</summary>
    private const int AllClearMessage = 6601;
    private const float AllClearRange = 100f;

    /// <summary>
    /// Enough hate to make the coffin-breaker his target on the spot.
    /// </summary>
    /// <remarks>
    /// Inferred: the pattern's <c>add_hate_point</c> carries no amount. Chosen to match the same
    /// action in <see cref="CloneOfBarrierAI"/> so one guess is not two.
    /// </remarks>
    private const int AlarmHate = 1000;

    /// <summary>The two calls the coffins answer, and the range retail gives them.</summary>
    public const int CallForPages = 6605;
    public const int CallForMore = 6607;
    private const float CallRange = 50f;

    /// <summary>Retail arms the wave timer at thirty-five seconds, then re-arms at forty-five.</summary>
    private const int FirstCallMillis = 35000;
    private const int CallIntervalMillis = 45000;

    private const int WavesStarted = 1;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail's timer-0 one-shot at 26-50. See the class remarks on why it hangs here.
        OnAttacked = Of(
            Branch(15, "light the fuse", [When.HpBetween(26, 50), When.FirstTime(WavesStarted)],
                Do.ArmTimer(11, FirstCallMillis))),

        OnBattleTimer = Of(
            Branch(3, "call", [When.Chance(50), When.Timer(11)],
                Do.ArmTimer(11, CallIntervalMillis),
                Do.Broadcast(CallForPages, CallRange)),

            Branch(2, "call", [When.Timer(11)],
                Do.ArmTimer(11, CallIntervalMillis),
                Do.Broadcast(CallForMore, CallRange))),

        OnMessage = Of(
            Branch(1, "", [When.Message(AlarmMessage)],
                Do.HateMessageTarget(AlarmHate))),

        OnDie = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(AllClearMessage, AllClearRange))),
    };

    public LordLannokAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
