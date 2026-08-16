using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The faithful subordinates and servants the Beluslan elemental bosses call up. Retail pattern
/// <c>ND2_PnF</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_retail_messages.py</c> — message <b>6505</b>, which
/// <see cref="FrostmaneLestinAI"/> was placing his three waves without.
/// <para>
/// <b>Every one of his summoning rungs broadcasts it.</b> All three — at 66–90, 41–65 and 21–40 —
/// place four elementals and then name his current target to fifty metres, and the wave that has just
/// arrived takes a hate point on that player and attacks. The same shape as Queen Modor's pillar trio,
/// and the same shared op: see <see cref="SummonOrder"/> for why the pair is reproduced rather than
/// collapsed into a target switch.
/// </para>
/// <list type="table">
/// <item><term>280489, 280490, 280491</term><description>Frostmane Lestin's three waves of
/// <b>faithful subordinates</b> — his own, and the reason this class exists</description></item>
/// <item><term>280333, 280334, 280335</term><description><b>faithful servants</b>, which share the
/// pattern and belong to the fire elemental boss below</description></item>
/// </list>
/// <para>
/// <b>The servants have no sender yet.</b> Their master is <c>ND2_ElementalSu</c> — raging kraterr
/// (211715) and its summoned twin (280332) — which runs on <c>summoner</c>, the generic table AI, and
/// broadcasts 6505 from its own summoning rungs in retail. Giving it that broadcast means translating
/// its pattern, which is a boss's worth of work rather than a branch's. They are listed here anyway
/// because it is one retail pattern, and splitting it would leave the next reader to rediscover that.
/// </para>
/// <para>
/// <b>Not translated:</b> the same pattern's handlers for <b>6506</b> and <b>6508</b>, which are
/// single <c>use_skill</c> branches, and everything else these NPCs do.
/// </para>
/// </remarks>
[AIName("elemental_wave")]
public class ElementalWaveAI : AggressiveNpcAI, INpcMessageListener
{
    /// <summary>
    /// Retail's message: the wave just placed goes to the player its master named. Named for the
    /// message rather than the op so it does not shadow <see cref="SummonOrder"/>.
    /// </summary>
    public const int OrderMessage = 6505;

    /// <summary>Retail's <c>range_as_meter</c>, on all three of his rungs.</summary>
    public const float OrderRange = 50f;

    public ElementalWaveAI(Npc owner)
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
