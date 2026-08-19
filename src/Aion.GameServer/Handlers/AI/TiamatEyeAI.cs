using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The gossip npcs the Tiamat Stronghold generals leave when they die (283177-283180).
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/TiamatEyeAI (@author Cheatkiller). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The switch was still keyed on the old ids.</b> It reads 283913-283916, and these npcs are
/// 283177-283180 — the same renumbering that had each general dropping another general's gossip npc,
/// corrected a few commits ago. So <b>not one of the four cases could ever match and none of them said
/// anything.</b>
/// </para>
/// <para>
/// <b>And they left after five seconds whatever retail says.</b> Retail's lifetimes are fifteen seconds
/// for Shavorkhan's, Sardha's and Rakshaka's and <b>ten</b> for Tahabata's; the fifteen given to all
/// three at the spawn site a few commits ago was inert, because this class's own clock is shorter and
/// the shorter clock always wins. Found by <c>audit_lifetime_conflicts.py</c>, which exists to find
/// exactly that — and found it in work from this same session.
/// </para>
/// </remarks>
[AIName("tiamateye")]
public class TiamatEyeAI : NpcAI
{
    public TiamatEyeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetOwner().GetNpcId())
        {
            case Shavorkhan:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500679, 2000);
                break;
            case Sardha:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500680, 2000);
                break;
            case Rakshaka:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500681, 2000);
                break;
            case Tahabata:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500682, 2000);
                break;
        }

        Despawn();
    }

    /// <summary>The four gossip npcs, in the order their generals fall.</summary>
    private const int Shavorkhan = 283177;
    private const int Sardha = 283178;
    private const int Rakshaka = 283179;
    private const int Tahabata = 283180;

    /// <summary>
    /// Retail's <c>live_time</c> on each: fifteen seconds, except Tahabata's, which is ten.
    /// </summary>
    private static readonly Dictionary<int, long> LifeMillis = new Dictionary<int, long>
    {
        [Shavorkhan] = 15_000L,
        [Sardha] = 15_000L,
        [Rakshaka] = 15_000L,
        [Tahabata] = 10_000L,
    };

    /// <summary>Anything else on this AI keeps the five seconds it always had.</summary>
    private const long DefaultLifeMillis = 5_000L;

    private void Despawn()
    {
        long life = LifeMillis.TryGetValue(GetOwner().GetNpcId(), out long known)
            ? known
            : DefaultLifeMillis;

        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!IsDead())
            {
                AIActions.DeleteOwner(this);
            }
        }, life);
    }
}
