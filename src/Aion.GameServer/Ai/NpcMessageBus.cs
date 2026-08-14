using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
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
        foreach (KnownObject known in sender.GetKnownList().Stream())
        {
            if (known.Get() is not Npc npc || npc == sender || npc.IsDead())
                continue;
            if (!PositionUtil.IsInRange(sender, npc, range))
                continue;
            if (npc.GetAi() is INpcMessageListener listener)
                listener.OnNpcMessage(sender, messageType, param);
        }
    }
}
