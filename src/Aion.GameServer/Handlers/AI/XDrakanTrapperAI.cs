using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Balaur officers of the Dredgion and Dark Poeta who lay a dragon's trap under whoever they are
/// fighting. Retail patterns <c>Dread_XDrakanReA</c>, <c>XDrakan_ReB_50</c> and
/// <c>Dread_SurkanaNm06</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Eight spawned npcs</b> — Baranath Triaris,
/// Auditor Nirshaka, Sentinel Garkusa, Prison Guard Mahnena and the four Anuhart officers — all on
/// plain <c>aggressive</c>. The three patterns differ only in their cast loops; the part that can be
/// translated is identical in all three, which is why one class covers them.
/// <list type="table">
/// <item><term>ten seconds in</term><description>it turns on the <b>second-most-hated</b> player, once
/// — the timer that carries it is never re-armed</description></item>
/// <item><term>crossing 70%</term><description>a <b>dragon's trap</b> (281161) goes down on whoever it
/// is fighting, five metres out, and it turns off the tank again</description></item>
/// </list>
/// <para>
/// <b>The trap lands on the player, not at the officer's feet.</b> <c>spawn_on_target</c> with
/// <c>OBJI_CUR_TARGET</c> and a fifty-metre validity — so a ranged group does not escape it, and a
/// tank that has just been peeled off is standing on it.
/// </para>
/// <para>
/// <b>The ten-second peel is once a fight and the seventy-percent one is once a band.</b> Retail
/// arms timer 1 on entering combat and never touches it again; the band rung carries a flag var and
/// re-arms only the six-second clock. So a fight has exactly two of these, and a raid that is pushed
/// through seventy before ten seconds are up still gets both.
/// </para>
/// <para>
/// <b>The band rung's re-arm of the six-second clock is carried and is not observable here.</b> Once
/// its flag is consumed there is nothing else on that slot but the bare fallback, so a dead clock and
/// a live one behave identically — the same situation recorded against the krall escape, arrived at
/// from the other direction: there two guards enforced one limit, here one guard makes the other
/// pointless. Left as a deliberate mutation survivor rather than given a pin that cannot fail.
/// </para>
/// <para>
/// <b>Not translated.</b> Seven skill indices and the branches that carry nothing else — the 76–100
/// and 36–70 cast loops on timer 2, the below-35 chain across timers 3, 4 and 5, and Garkusa's extra
/// 90–100 loop on timer 4. The <c>say_to_all</c> lines, which have no <c>npc_shouts.xml</c> row.
/// </para>
/// <para>
/// <b>And three events we do not raise at all</b>, which between them carry the rest of these
/// patterns: <c>on_see_friend_attacked</c> and <c>on_friend_spelled</c> — each turns the officer onto
/// whoever touched its neighbour, once a fight — and <c>on_enter_abnormal_state</c>, which broadcasts
/// <c>3403</c> or <c>6836</c> ten metres when the officer is crowd-controlled. That last one is a
/// genuine mechanic (a stunned Balaur calling its friends) and it has no listener in our data either,
/// so it is recorded twice over: no event to raise it, and nobody to hear it.
/// </para>
/// </remarks>
[AIName("xdrakan_trapper")]
public class XDrakanTrapperAI : PatternAi
{
    /// <summary><c>BXDrakan_ReB_Trap_50_An</c> — a dragon's trap.</summary>
    private const int DragonsTrap = 281161;

    /// <summary>Retail's <c>SPAWN_ID_1</c> and its <c>spawn_range</c>.</summary>
    private const int Laid = 1;
    private const float Reach = 5f;

    private const int Heartbeat = 0;
    private const int Opening = 1;

    /// <summary>Retail's ALPHA_1: the band rung is once a fight.</summary>
    private const int Below70 = 1;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 2, which drives cast loops only.
        OnEnterAttack = Of(
            Branch(11, "", When.Always,
                Do.ArmTimer(Heartbeat, 6000),
                Do.ArmTimer(Opening, 10000))),

        OnBattleTimer = Of(
            Branch(6, "36-70 lays a trap and peels", [When.Timer(Heartbeat), When.HpBetween(36, 70),
                    When.FirstTime(Below70)],
                Do.ArmTimer(Heartbeat, 6000),
                Do.SpawnOnTarget(DragonsTrap, Laid, count: 1, range: Reach),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            // Never re-armed, so this is the one peel every fight opens with.
            Branch(4, "the opening peel", [When.Timer(Opening)],
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(1, "", [When.Timer(Heartbeat)],
                Do.ArmTimer(Heartbeat, 6000))),
    };

    public XDrakanTrapperAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
