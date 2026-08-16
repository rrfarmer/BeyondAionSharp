using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The three summons Queen Modor calls to the pillar — Modor's bodyguard (284380), the vengeful
/// reaper (284381) and the hoarfrost acheron drake (284382). Retail patterns
/// <c>Rune_FrostNmd_DealSum2_65_Ae</c>, <c>Rune_FrostNmd_TankSum2_65_Ae</c> and
/// <c>Rune_FrostNmd_MezSum2_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_retail_messages.py</c> — message <b>444</b>, which
/// <see cref="CursedQueenModorAI"/> was placing these three without.
/// <para>
/// <b>They arrive unassigned, and the order assigns them.</b> Every branch of Modor's that spawns the
/// trio broadcasts 444 fifty metres naming <em>her current target</em>, and all six summon patterns
/// answer it the same way: <c>add_hate_point</c> on whoever she named, then <c>attack_most_hating</c>.
/// A summon that has just appeared holds no hate at all, so one point is enough to make her target
/// the most-hated — which is the whole mechanic. She does not merely call three adds; she calls three
/// adds <em>onto a named player</em>.
/// </para>
/// <para>
/// <b>Deliberately one point, and deliberately most-hated rather than the named player</b> — see
/// <see cref="SummonOrder"/>, which is the shared op, and which the same branch in Frostmane Lestin's
/// elementals uses.
/// </para>
/// <para>
/// <b>Why a listener rather than a pattern.</b> These three run plain <c>aggressive</c> and their full
/// rotations are <c>SKILLI_INDEX</c> chains this work cannot resolve — unlike their <c>Sum</c>
/// siblings 284377 and 284378, whose branch comments named their skills and which
/// <see cref="DanuarFrostSummonAI"/> translates in full. Adding the one branch that is index-free
/// leaves the rest of their behaviour exactly as it was.
/// </para>
/// <para>
/// <b>Not translated:</b> everything else in those six patterns, which is casts; and message
/// <b>104</b>, the other number the audit reports on this family — its listeners are the Dramata
/// drakan rather than these summons, and its senders are patterns this work has not read.
/// </para>
/// </remarks>
[AIName("danuar_summon_order")]
public class DanuarSummonOrderAI : AggressiveNpcAI, INpcMessageListener
{
    /// <summary>
    /// Retail's message: the three she just called go to the player she named. Named for the message
    /// rather than the op so it does not shadow <see cref="SummonOrder"/> at the call site below.
    /// </summary>
    public const int OrderMessage = 444;

    /// <summary>
    /// Retail's <c>range_as_meter</c> on the broadcast. Kept beside the message rather than with the
    /// sender: the two are one fact about the order, and a sender that invented its own range would
    /// be a different mechanic.
    /// </summary>
    public const float OrderRange = 50f;

    public DanuarSummonOrderAI(Npc owner)
        : base(owner)
    {
    }

    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != OrderMessage || IsDead())
            return;

        SummonOrder.Take(GetOwner(), param);
    }
}
