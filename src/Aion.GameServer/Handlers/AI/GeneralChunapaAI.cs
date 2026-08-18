using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// General Chunapa (218183), Cygnea. Retail pattern <c>LDF4a_SandWarm_General</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. LEGENDARY, on a nine-hour respawn, on plain
/// <c>aggressive</c>. The shirik burrow (282556, ELITE) he opens was spawned by nothing anywhere.
/// <para>
/// <b>Phase two opens burrows under the raid.</b> Between 51 and 75 he puts one under each of the
/// <b>two most-hated</b> players within seventy-five metres, and does it again every forty-five
/// seconds for as long as the fight stays in that band. Each burrow lasts just over a minute. Below 51
/// the branch stops matching and no more open.
/// </para>
/// <para>
/// <b>Heartbeats that switch themselves off.</b> Retail runs four three-second timers whose only job
/// is to notice a phase boundary, and the phase branch that answers each one does <i>not</i> re-arm
/// it — so the heartbeat stops the moment it has done its work. That is what makes an unflagged
/// branch fire once: timer 0 ticks every three seconds until health first goes under 75, at which
/// point its phase branch lights timer 1 and timer 0 goes quiet. The port keeps that shape, because
/// the route into the burrow band runs through it.
/// </para>
/// <para>
/// <b>The casts are not translated, and with them three of the four phases.</b> His branch comments
/// are unusually good — 가시분출 "thorn burst", 소화액 "digestive fluid", 스턴 "stun", 격노 "rage",
/// 처형 "execution" on whoever has the least health — but <b>he has no <c>npc_skills</c> entry at
/// all</b>, so there is nothing for any of those names to map onto. Good comments do not help when
/// the other half of the mapping is missing; this is the Golden Tatar case, not the Derakanak one.
/// Omitted with them: phase one's thorn-burst and digestive-fluid timers, phase three's pair, and
/// phase four's paralysis and execution timers, together with the stuns that mark each transition.
/// </para>
/// <para>
/// <b>Also not translated:</b> the door he controls on engaging, whose method semantics are still
/// unresolved (see the correction entry above), and his five shouts, which have no numeric ids in our
/// data.
/// </para>
/// </remarks>
[AIName("general_chunapa")]
public class GeneralChunapaAI : PatternAi
{
    private const int ShirikBurrow = 282556;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Burrows = 1;

    private const int BurrowLife = 64;

    private static readonly PatternCondition BurrowBand = When.HpBetween(51, 75);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timers 2 through 6 here, all of them cast-only.
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, 3000),
                Do.ArmTimer(1, 3000))),

        OnBattleTimer = Of(
            // Phase two's transition. It carries a stun and a shout in retail; what survives here is
            // that it lights timer 1 and, by not re-arming timer 0, stops the heartbeat that found it.
            Branch(40, "into phase two", [When.Timer(0), When.HpBelow(75)],
                Do.ArmTimer(1, 3000)),

            Branch(24, "burrows", [When.HpBetween(51, 75), When.Timer(1), BurrowBand],
                Do.SpawnOnEachTarget(ShirikBurrow, Burrows, validDistance: 75f, maxTargets: 2,
                    MultiTargetOrder.Descending, range: 2f, liveSeconds: BurrowLife),
                Do.ArmTimer(1, 45000)),

            // The heartbeat that watches for 75. Lower priority than the branch above, so it only
            // runs while he is still above it.
            Branch(10, "", [When.Timer(0)],
                Do.ArmTimer(0, 3000))),

        OnLeaveAttack = Of(
            Branch(7, "", When.Always, Do.Despawn(Burrows))),

        OnDie = Of(
            Branch(100, "", When.Always, Do.Despawn(Burrows))),
    };

    public GeneralChunapaAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
