using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Ai;

/// <summary>
/// An NPC that reacts to messages broadcast by other NPCs.
/// </summary>
/// <remarks>
/// Retail AI patterns wire encounters together with a <c>broadcast_message</c> action and a
/// matching <c>on_message</c> handler: an integer message type, an optional object parameter,
/// and a broadcast radius. It is how an add tells its boss which player it just debuffed, how a
/// trigger NPC tells a boss to leave, and how phase transitions propagate. Nothing equivalent
/// exists in aionemu, so encounters that depend on it are simply missing.
/// <para>
/// Message type numbers are chosen per encounter by the original designers and have no global
/// registry, so listeners must only act on the numbers their own pattern uses.
/// </para>
/// </remarks>
public interface INpcMessageListener
{
    /// <param name="sender">The NPC that broadcast the message.</param>
    /// <param name="messageType">Designer-assigned message id, scoped to one encounter.</param>
    /// <param name="param">Optional object the message refers to, e.g. a targeted player.</param>
    void OnNpcMessage(Npc sender, int messageType, VisibleObject? param);
}

/// <summary>
/// Delivers retail <c>broadcast_message</c> actions to nearby NPCs.
/// </summary>
public static class NpcMessageBus
{
    /// <summary>
    /// Delivers <paramref name="messageType"/> to every living NPC within
    /// <paramref name="range"/> metres of <paramref name="sender"/> whose AI listens for
    /// messages. The sender never receives its own broadcast.
    /// </summary>
    public static void Broadcast(Npc sender, int messageType, VisibleObject? param, float range)
    {
        foreach (VisibleObject candidate in Nearby(sender))
        {
            if (candidate is not Npc npc || npc == sender || npc.IsDead())
                continue;
            if (!PositionUtil.IsInRange(sender, npc, range))
                continue;
            if (npc.GetAi() is INpcMessageListener listener)
                listener.OnNpcMessage(sender, messageType, param);
        }
    }

    /// <summary>
    /// The sender's known list, or its map region when that list is still empty.
    /// </summary>
    /// <remarks>
    /// <c>World.Spawn</c> raises the AI's spawned event from <c>OnAfterSpawn</c> and only builds the
    /// known list afterwards, so an NPC that broadcasts from <c>on_wake_up</c> has nothing to talk to.
    /// The Java reference has the same ordering, so the fix is here rather than in the spawn path:
    /// reordering that would diverge from upstream on code every spawn in the server passes through.
    /// <para>
    /// The fallback is gated on the list being <i>empty</i>, which in practice means a just-spawned
    /// NPC. Every broadcast from a battle timer, a death or a message runs with a populated list and
    /// takes the original path untouched, so the region scan is not on the warm path.
    /// </para>
    /// <para>
    /// It scans the sender's own region only. Neighbouring regions were tried and dropped: retail's
    /// wake-up broadcasts carry ranges of fifty metres or less against regions far larger, so the
    /// extra breadth was untestable here and would have been code no pin could reach. A wake-up
    /// broadcast whose sender sits close enough to a region edge to need it will under-deliver, and
    /// that is the known limit of this fallback.
    /// </para>
    /// </remarks>
    private static IEnumerable<VisibleObject> Nearby(Npc sender)
    {
        IEnumerable<KnownObject> known = sender.GetKnownList().Stream();
        if (known.Any())
        {
            foreach (KnownObject k in known)
                if (k.Get() is VisibleObject seen)
                    yield return seen;
            yield break;
        }

        MapRegion? region = sender.GetPosition()?.GetMapRegion();
        if (region == null)
            yield break;

        foreach (VisibleObject o in region.GetObjects().Values)
            yield return o;
    }
}
