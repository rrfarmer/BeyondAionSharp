using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Ai;

/// <summary>
/// Retail's <c>on_see_friend_attacked</c> and <c>on_friend_spelled</c>: an NPC watching one of its own
/// take a hit.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>These are the two largest events retail has and
/// aionemu does not</b> — <c>on_see_friend_attacked</c> appears in <b>397</b> patterns of the 5.8 files
/// and <c>on_friend_spelled</c> in <b>344</b>, against 129 for the friend-killed handler that was built
/// first. Nearly everything a camp does when one of its members is jumped hangs off them.
/// <para>
/// <b>The audience is decided exactly as <see cref="FriendDeathNotice"/> decides it</b>, and for the
/// same reasons: the victim's own known list, each observer's own <c>srange</c> — the range belongs to
/// the eye — and <see cref="TribeRelationService.IsFriend"/> for what "friend" means. Sharing those
/// three decisions matters more than the decisions themselves; two notices with different audiences
/// would be a bug nobody could see.
/// </para>
/// <para>
/// <b>Unlike the death notice, this fires on every blow</b>, which is why it carries a re-entrancy
/// guard. A watcher's answer is usually to take hate on the attacker, that hate raises an attack event
/// of its own, and without the guard a camp of mutually-watching NPCs would notify itself until the
/// engine's recursion cut-off fired. Retail's own branches are nearly always flagged to fire once,
/// which hides the problem in the data rather than solving it.
/// </para>
/// <para>
/// <b>Cost:</b> the damage path already walks the victim's known list on every hit, to raise
/// <c>CreatureNeedsSupport</c>. This walks the same list beside it.
/// </para>
/// </remarks>
public static class FriendCombatNotice
{
    [System.ThreadStatic]
    private static bool raising;

    /// <summary>
    /// Tells every friendly NPC that could see <paramref name="victim"/> that it has been hit.
    /// </summary>
    /// <param name="spelled">
    /// A skill rather than a swing, which is the whole difference between retail's two handlers. The
    /// damage path can tell them apart only by whether an <c>Effect</c> came with the blow.
    /// </param>
    public static void Raise(Creature victim, Creature? attacker, bool spelled)
    {
        if (attacker == null || victim is not Npc hurt || hurt.IsDead())
            return;

        // See the class remarks: a watcher's answer lands back in the damage path.
        if (raising)
            return;

        raising = true;
        try
        {
            foreach (VisibleObject candidate in NpcMessageBus.Nearby(hurt))
            {
                if (candidate is not Npc watcher || watcher == hurt || watcher.IsDead())
                    continue;

                if (!TribeRelationService.IsFriend(watcher, hurt))
                    continue;

                int sight = watcher.GetObjectTemplate().GetAggroRange();
                if (sight <= 0 || !Aion.GameServer.Utils.PositionUtil.IsInRange(watcher, hurt, sight))
                    continue;

                if (watcher.GetAi() is Pattern.PatternAi pattern)
                    pattern.NoteFriendInTrouble(hurt, attacker);

                watcher.GetAi().OnCreatureEvent(
                    spelled ? AiEventType.FriendSpelled : AiEventType.FriendAttacked, hurt);
            }
        }
        finally
        {
            raising = false;
        }
    }
}
