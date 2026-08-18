using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Ai;

/// <summary>
/// Retail's <c>on_see_friend_killed_by_user</c>: an NPC watching one of its own go down.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. This is one of the larger events retail has and
/// aionemu does not — <b>129 patterns in the 5.8 files carry the handler, with 377 npcs bound to
/// them</b> — and what hangs off it is almost always the same thing: the survivors leave. Commander
/// Bakarma's promotion ladder is built around it, and it was the reason that ladder shipped without
/// its counter-play.
/// <para>
/// <b>Three decisions, and none of them is a made-up number.</b>
/// </para>
/// <para>
/// <b>Who hears it</b> is the dead NPC's own known list, which is how every other broadcast on this
/// server finds its audience.
/// </para>
/// <para>
/// <b>How far</b> is each <em>observer's</em> own <c>srange</c> — the sight range on its template —
/// rather than one radius chosen for all of them. Retail's event is a seeing event, so the range
/// belongs to the eye and not to the corpse, and a bigfoot kerubar with forty metres of sight really
/// does see further than a klaw with eight. Picking a single constant here was the objection that kept
/// this out of the Bakarma commit; the observer's own template answers it.
/// </para>
/// <para>
/// <b>Who counts as a friend</b> is <see cref="TribeRelationService.IsFriend"/>, the same test the
/// aggro layer uses. Retail's word is <c>friend</c> and this is what the word already means here.
/// </para>
/// <para>
/// Players are never notified — the event is <c>killed_by_user</c>, so the watcher is an NPC by
/// definition, and the killer being a player is checked by the caller.
/// </para>
/// </remarks>
public static class FriendDeathNotice
{
    /// <summary>
    /// Tells every friendly NPC that could see <paramref name="dead"/> that it has fallen.
    /// </summary>
    /// <param name="killer">
    /// Whoever landed the killing blow. Retail's handler is <c>..._killed_by_user</c>, so this fires
    /// only for a player kill: an add finished off by another NPC, by its own <c>live_time</c>, or by
    /// a boss clearing the board is not what the handler is about.
    /// </param>
    public static void Raise(Npc dead, Creature? killer)
    {
        if (killer is not Player)
            return;

        foreach (VisibleObject candidate in NpcMessageBus.Nearby(dead))
        {
            if (candidate is not Npc watcher || watcher == dead || watcher.IsDead())
                continue;

            if (!TribeRelationService.IsFriend(watcher, dead))
                continue;

            // The eye's range, not the corpse's: retail's event is about being seen to fall.
            int sight = watcher.GetObjectTemplate().GetAggroRange();
            if (sight <= 0 || !Aion.GameServer.Utils.PositionUtil.IsInRange(watcher, dead, sight))
                continue;

            // Retail's OBJI_KILLER: named by a third of the branches on this handler, so it has to
            // reach the watcher. Cleared again by PatternAi once the branches have run.
            if (watcher.GetAi() is Pattern.PatternAi pattern)
                pattern.NoteFriendsKiller(killer);

            watcher.GetAi().OnCreatureEvent(AiEventType.FriendKilled, dead);
        }
    }
}
