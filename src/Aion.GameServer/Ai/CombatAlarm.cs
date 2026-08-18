using System.Threading;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai;

/// <summary>
/// One retail <c>broadcast_message</c> raised the first time an NPC is drawn into a fight.
/// </summary>
/// <remarks>
/// <see cref="Pattern.PatternAi"/> gets <c>on_enter_attack_state</c> for free, because it latches the
/// transition itself and evaluates a whole handler there. A Java-parity class has no such handler and
/// no such latch: it sees <c>HandleAttack</c> on every swing, so a broadcast written there would go out
/// several times a second.
/// <para>
/// This is the smallest thing that closes the gap. The Sauro Supply Base's two bosses — Brigade General
/// Sheba and Guard Captain Ahuradim — each raise the base alarm as they engage, and their guards answer
/// it by taking whoever the boss is fighting. Both bosses are Java ports of aionemu classes that never
/// had the alarm, so this is an addition to them rather than a translation of them, and it is kept to
/// one field and two calls so that the Java behaviour beside it stays legible.
/// </para>
/// <para>
/// <see cref="Rearm"/> belongs on the same events a pattern would reset on — going home and dying —
/// so a second pull raises the alarm again.
/// </para>
/// </remarks>
public sealed class CombatAlarm
{
    private readonly int messageType;
    private readonly float range;
    private int raised;

    public CombatAlarm(int messageType, float range)
    {
        this.messageType = messageType;
        this.range = range;
    }

    /// <summary>Call from <c>HandleAttack</c>; only the first call of a fight does anything.</summary>
    /// <remarks>
    /// Retail's own parameter is <c>OBJI_CUR_TARGET</c>, so the alarm names the player the boss is
    /// fighting rather than the one who happened to land the blow.
    /// </remarks>
    public void Raise(Npc owner)
    {
        if (Interlocked.CompareExchange(ref raised, 1, 0) != 0)
            return;

        NpcMessageBus.Broadcast(owner, messageType, owner.GetTarget(), range);
    }

    /// <summary>Call from the handlers that end a fight, so the next pull raises it again.</summary>
    public void Rearm() => Interlocked.Exchange(ref raised, 0);
}
