using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Ai.Pattern;
using static Aion.GameServer.Ai.Pattern.AiPattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Siege;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The artifact and fortress protectors. Retail patterns <c>AB1_LDGuard_Artifact</c>,
/// <c>AB1_DrGuard_Artifact</c> and the <c>LDF5_Fortress_*_Artifact</c> pair.
/// </summary>
/// <remarks>
/// Java parity: ai/siege/AbstractSiegeProtectorAI. Retail-sourced addition below; see
/// docs/retail-ai-fidelity.md, and <c>tools/client-extract/audit_npc_call_family.py</c> for the size of
/// what this is one half of.
/// <para>
/// <b>These guards fight the fortress killer, and here they ignored it.</b> Retail's second call family
/// is npc-versus-npc and works the opposite way to <see cref="AbyssGuardCallAI"/>'s 23000: the message
/// names its <b>sender</b> and carries <c>points_to_add=1000000</c>. A fortress killer broadcasts
/// <b>30001</b> as it wakes, and every protector within fifty metres drops what it is doing and goes for
/// it. That is not a nudge like 23000's single point — it is how a fortress changes hands without a
/// player touching either side, and none of it happened here.
/// </para>
/// <para>
/// <b>And a protector announces its own death.</b> Retail's <c>on_die</c> broadcasts <b>30003</b> at
/// fifty metres naming itself, and the killer that was hunting it answers by despawning. Both ends
/// exist now: see <see cref="FortressKillerAI"/>, which sends the 30001 this class answers and answers
/// the 30003 this class sends.
/// </para>
/// <para>
/// <b>Not translated:</b> the protectors' own <c>30002</c> broadcast, which retail sends from a battle
/// timer inside a cast chain (<c>BTIMERI_INDEX_3</c>, re-arming <c>INDEX_1</c> at 7500) and so is
/// behind the skill index like the rest of their ladder.
/// </para>
/// </remarks>
public abstract class AbstractSiegeProtectorAI : SiegeNpcAI, INpcMessageListener
{
    /// <summary>Retail's killer-wakes call, and the range every protector pattern answers it at.</summary>
    public const int KillerAwake = 30001;

    /// <summary>Retail's <c>on_die</c> broadcast, and its range.</summary>
    /// <remarks>
    /// <b>Only a minority of protectors send it.</b> This class is bound to 1,219 npcs running 93
    /// distinct retail patterns, and two of those patterns carry the broadcast; counting the village
    /// chiefs and the arena, 475 npcs in the whole dump announce their death this way. The rest die
    /// quietly, and since <see cref="FortressKillerAI"/> answers 30003 by standing down, sending it for
    /// all 1,219 was calling fortress killers off fights retail leaves running.
    /// <para>
    /// <see cref="DeathCallRange"/> is kept for the callers that still name it, but the range actually
    /// used comes from <see cref="SiegeDeathCalls"/>, per npc, out of the pattern.
    /// </para>
    /// </remarks>
    public const int ProtectorDown = 30003;
    public const float DeathCallRange = 50f;

    /// <summary>
    /// Retail's <c>points_to_add</c> on both rungs of this family. A million, against 23000's one.
    /// </summary>
    public const int DropEverything = 1_000_000;

    /// <summary>Retail's <c>30002</c>: "the killer should be fighting me".</summary>
    public const int CallTheKiller = 30002;

    /// <summary>
    /// One pattern per protector, because the cadence differs and a fortress holds hundreds.
    /// </summary>
    /// <remarks>
    /// <b>The middle message of the loop, and it was never sent.</b> Retail hangs 30002 off a
    /// battle-timer chain several rungs deep — the artifact guards reach it 21.5 seconds into a fight
    /// and every 22 thereafter, their balaur twins at 27.5 and 28, and the village chiefs the moment
    /// they are engaged and every 5 seconds after. <see cref="ProtectorCalls"/> is those numbers, walked
    /// out of the chains rather than fitted to one of them.
    /// <para>
    /// What hung off the same rungs and is still absent is the cast ladder: every one of these chains
    /// interleaves <c>use_skill</c> with the timer hand-offs. Dropping those does not change when the
    /// broadcast lands, which is why the cadence is a faithful reduction rather than an approximation.
    /// </para>
    /// </remarks>
    private readonly AiPattern pattern;

    public AbstractSiegeProtectorAI(Npc owner)
        : base(owner)
    {
        pattern = ProtectorCalls.PatternFor(owner.GetNpcId());
    }

    protected override AiPattern Pattern => pattern;

    /// <summary>
    /// Retail's two <c>on_message</c> rungs for <c>30001</c>, both keyed on the <b>sender</b>: a
    /// protector already fighting switches to it, one standing about takes the hate and goes.
    /// </summary>
    /// <remarks>
    /// Both come through <see cref="SummonOrder"/> with retail's own value. At a million points the two
    /// rungs land in the same place — whoever is then most-hated is the caller either way — which is
    /// exactly why retail can afford to write them as one number and two guards.
    /// </remarks>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != KillerAwake || IsDead() || sender == GetOwner())
            return;

        SummonOrder.Take(GetOwner(), sender, DropEverything);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        GetAggroList().Clear(); // make sure old damages aren't counted in stopSiege
    }

    /// <summary>
    /// Retail's <c>on_die</c>: tell the fifty metres around it that this one is gone — <b>if this npc is
    /// one of the ones that does.</b>
    /// </summary>
    /// <remarks>
    /// <b>Broadcast first.</b> It is retail's own order — the broadcast is the first action in the
    /// rung — and it also matters here: everything after it reaches the siege services, and a protector
    /// dying outside a live siege must still tell the killer hunting it to stand down.
    /// <para>
    /// <b>And only for the npcs whose pattern carries it.</b> See <see cref="SiegeDeathCalls"/>: the
    /// unconditional version of this line was a faithful port of the Java class and wrong against
    /// retail for 877 of the 1,219 npcs bound here, which is invisible from the C# because nothing in
    /// the C# is inconsistent — the check has to be made against the patterns of the npcs bound to it.
    /// </para>
    /// </remarks>
    protected override void HandleDied()
    {
        SiegeDeathCalls.Announce(GetOwner());

        base.HandleDied();
        StopSiege((SiegeNpc)GetOwner());
    }

    internal static void StopSiege(SiegeNpc siegeProtector)
    {
        Siege siege = SiegeService.GetInstance().GetSiege(siegeProtector.GetSiegeId());
        foreach (AggroInfo aggroInfo in siegeProtector.GetAggroList().Stream())
            siege.GetSiegeCounter().AddDamage(aggroInfo.GetAttacker().GetMaster(), aggroInfo.GetDamage());
        siege.SetBossKilled(true);
        SiegeService.GetInstance().StopSiege(siege.GetSiegeLocationId());
    }
}
