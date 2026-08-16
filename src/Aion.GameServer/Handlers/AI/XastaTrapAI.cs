using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Xasta's trap (282444). Retail pattern <c>IDYun_Drakan_ND5</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It was on the generic <c>trap</c> AI, which made it
/// a trap — and this one's job is not to be a trap. <b>It is the clock for his second form.</b>
/// <para>
/// The whole chain: Xasta drops one on a random attacker; it lives thirteen seconds; and as it goes it
/// broadcasts <b>200</b> to a hundred metres, which is the only thing that re-arms his trap timer.
/// Without this broadcast his second form drops one trap and never another — the branch that spawns it
/// does not re-arm itself.
/// </para>
/// <para>
/// <b>Not translated:</b> its damage-over-time tick (a <c>SKILLI_INDEX</c> every five seconds) and the
/// message <b>100</b> it broadcasts on engaging. 100 is a real pairing — Xasta answers it with an
/// eight-cast combo — but every one of those casts is index-only, so sending it would reach a listener
/// with nothing to do. Recorded rather than wired: it is the same sender-with-no-useful-listener shape
/// as the fortress lords' despawn helpers, and it becomes worth sending the day those indices resolve.
/// </para>
/// </remarks>
[AIName("xasta_trap")]
public class XastaTrapAI : PatternAi
{
    /// <summary>A hundred metres, which is retail's range and the whole room.</summary>
    private const float Reach = 100f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnDespawn = Of(
            Branch(3, "Set Timer", When.Always,
                Do.Broadcast(CaptainXastaAI.TrapGone, Reach))),
    };

    public XastaTrapAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
