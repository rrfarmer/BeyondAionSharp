using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Deputy Hanuman (212306) and Missing Indratu (280751), the Drakan camp captains of Heiron. Retail
/// pattern <c>NDrakan_KhB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two LEGENDARY bosses sharing one pattern, both on
/// plain <c>aggressive</c> with no class at all — the second of the three spawn-carrying named bosses
/// left at the top of <c>tools/client-extract/audit_missing_ai.py</c>.
/// <list type="table">
/// <item><term>91–100</term><description>the six-second clock, and casts</description></item>
/// <item><term>71–90</term><description>two <b>faithful subordinates</b> (280752), ten metres out,
/// thirty minutes</description></item>
/// <item><term>51–70</term><description>two more, into the same group</description></item>
/// <item><term>31–50</term><description>every subordinate <b>changes</b>, and five seconds later two
/// already-changed ones arrive</description></item>
/// <item><term>below 30</term><description>they change a <b>second</b> time, the clock stops, and he
/// starts hunting whoever is closest to dying</description></item>
/// </list>
/// <para>
/// <b>The adds are not four, then six, then eight — they are the same adds, three times over.</b> The
/// three summons share a display name and differ only in id: 280752, 280753, 280754. Entering 31–50 he
/// broadcasts <c>5001</c> and every subordinate still standing sheds itself for the next form two
/// seconds later (<see cref="HanumanSubordinateAI"/>); entering the last thirty he broadcasts
/// <c>5002</c> and they do it again, instantly. So a raid that killed the adds each time meets four
/// weak ones; a raid that ignored them meets six of the strongest.
/// </para>
/// <para>
/// <b>And he peels.</b> Each band arms its own alarm, and when it rings he shouts, tells the whole
/// group to re-pick, and turns on the <b>third-most-hated</b> player — the one behind the tank and the
/// off-tank. The 71–90 and 31–50 alarms alternate between two timers so they keep ringing for as long
/// as the band lasts; the 51–70 one rings once and then hands over to a cast loop. Below thirty the
/// pick changes: every twenty-eight seconds he goes for the <b>lowest health fraction</b> in the room,
/// which is what makes the last third the dangerous one.
/// </para>
/// <para>
/// <b>The ladder stops itself.</b> The below-thirty rung is the only one that does not re-arm timer 0,
/// so once it fires there are no more waves and no more changes — only the hunt. Every rung above it
/// carries a flag var, so each band gives its pair exactly once however long you stand in it.
/// </para>
/// <para>
/// <b>Not translated.</b> Eight skill indices and the four branches that are nothing but casts — the
/// 91–100 timer-1 loop, the 51–70 timer-4 loop, and the cast halves of the peel rungs. The four
/// <c>say_to_all</c> lines: retail's string ids (<c>STR_CHAT_CoDragon_AIPattern_4</c>, <c>_33</c>,
/// <c>_57</c>, <c>_59</c>) have no row in <c>npc_shouts.xml</c> for any of these five npcs, so there is
/// nothing to say them with. The <c>6001</c> sent below thirty, whose only audience by then is 280754,
/// whose pattern answers with a cast. And <c>despawn_at_attack_state</c> on all three spawns, which we
/// leave to <c>live_time</c> for the reason recorded against the Abyssal Reliquary flying worm: retail
/// declares no despawn handler here, so inventing one would be our behaviour and not theirs.
/// </para>
/// </remarks>
[AIName("deputy_hanuman")]
public class DeputyHanumanAI : PatternAi
{
    /// <summary><c>BLF3_NM_DrakanDF3Slave1_48_Ae</c> — the form the first two waves arrive in.</summary>
    private const int Subordinate1 = 280752;

    /// <summary><c>BLF3_NM_DrakanDF3Slave2_48_Ae</c> — what they become, and what 31–50 adds.</summary>
    private const int Subordinate2 = 280753;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: every wave lands in one group.</summary>
    private const int Group = 1;

    private const int PerWave = 2;

    /// <summary>Retail's <c>spawn_range</c> and its <c>range_as_meter</c>.</summary>
    private const float Ring = 10f;
    private const float Reach = 50f;

    /// <summary>Retail's <c>live_time</c>: thirty minutes on the first form, twenty on the second.</summary>
    private const int FirstLife = 1800;
    private const int SecondLife = 1200;

    // Retail's battle timer indices, kept as its own numbers so the pattern reads against the dump.
    private const int Ladder = 0;
    private const int HighAlarm = 2;
    private const int HighAlarmBack = 3;
    private const int MidAlarm = 5;
    private const int Wave = 6;
    private const int LowAlarm = 7;
    private const int LowAlarmBack = 8;
    private const int Hunt = 9;

    // Retail's ALPHA_1, BETA_1, ALPHA_2, ALPHA_3.
    private const int Below90 = 1;
    private const int Below70 = 2;
    private const int Below50 = 3;
    private const int Below30 = 4;

    private const int HeartbeatMillis = 6000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 1 -- the 91-100 cast loop -- and casts on itself.
        OnEnterAttack = Of(
            Branch(11, "", When.Always,
                Do.ArmTimer(Ladder, HeartbeatMillis))),

        OnBattleTimer = Of(
            // The hunt. Armed only by the below-30 rung, and re-arms itself, so it runs to the end.
            Branch(14, "hunt the weakest", [When.Timer(Hunt)],
                Do.ArmTimer(Hunt, 28000),
                Do.SwitchTarget(AggroTarget.LOWEST_HP)),

            // Does not re-arm timer 0: the ladder ends here, and with it every further wave.
            Branch(13, "below 30", [When.Timer(Ladder), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(Hunt, 18000),
                Do.Broadcast(HanumanSubordinateAI.ChangeAgain, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            // The far half of the 31-50 alarm. Its re-arm of the ladder is retail's, and lands on the
            // fallback below -- by this band the rung it would reach has already been spent.
            Branch(12, "31-50 alarm resets", [When.Timer(LowAlarmBack), When.HpBetween(31, 50)],
                Do.ArmTimer(Ladder, 10000),
                Do.ArmTimer(LowAlarm, 22000)),

            Branch(11, "31-50 alarm", [When.Timer(LowAlarm), When.HpBetween(31, 50)],
                Do.ArmTimer(LowAlarmBack, 25000),
                Do.Broadcast(HanumanSubordinateAI.PickAnother, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            // The pair that arrives already changed, five seconds after the band opens. One shot: its
            // timer is armed by the rung below and never re-armed.
            Branch(10, "31-50 wave", [When.Timer(Wave), When.HpBetween(31, 50)],
                Do.SpawnNear(Subordinate2, Group, count: PerWave, range: Ring, liveSeconds: SecondLife)),

            Branch(9, "31-50 opens", [When.Timer(Ladder), When.HpBetween(31, 50), When.FirstTime(Below50)],
                Do.ArmTimer(Ladder, 7000),
                Do.ArmTimer(Wave, 5000),
                Do.ArmTimer(LowAlarm, 30000),
                Do.Broadcast(HanumanSubordinateAI.ChangeOnce, Reach, aboutTarget: true)),

            // Rings once: retail's far half of this alarm is a cast loop on its own timer, which never
            // hands back, so the peel does not repeat inside this band.
            Branch(7, "51-70 alarm", [When.Timer(MidAlarm), When.HpBetween(51, 70)],
                Do.Broadcast(HanumanSubordinateAI.PickAnother, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(6, "51-70", [When.Timer(Ladder), When.HpBetween(51, 70), When.FirstTime(Below70)],
                Do.ArmTimer(Ladder, 8000),
                Do.ArmTimer(MidAlarm, 20000),
                Do.SpawnNear(Subordinate1, Group, count: PerWave, range: Ring, liveSeconds: FirstLife)),

            // The far half of the 71-90 alarm: nothing but the hand-back, which is what keeps that
            // band's peel repeating every forty-five seconds instead of happening once.
            Branch(5, "71-90 alarm resets", [When.Timer(HighAlarmBack), When.HpBetween(71, 90)],
                Do.ArmTimer(HighAlarm, 20000)),

            Branch(4, "71-90 alarm", [When.Timer(HighAlarm), When.HpBetween(71, 90)],
                Do.ArmTimer(HighAlarmBack, 25000),
                Do.Broadcast(HanumanSubordinateAI.PickAnother, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(3, "71-90", [When.Timer(Ladder), When.HpBetween(71, 90), When.FirstTime(Below90)],
                Do.ArmTimer(Ladder, 8000),
                Do.ArmTimer(HighAlarm, 25000),
                Do.SpawnNear(Subordinate1, Group, count: PerWave, range: Ring, liveSeconds: FirstLife)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, HeartbeatMillis))),
    };

    public DeputyHanumanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The faithful subordinates Hanuman calls, in their first two forms (280752 and 280753). Retail
/// patterns <c>NDrakan_ChSlave4</c> and <c>NDrakan_Chslave5</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>An add that survives a band does not stay what it was.</b> On <c>5001</c> the first form arms a
/// two-second fuse and then leaves a second-form subordinate (280753) where it stood; on <c>5002</c>
/// the second form does the same for the third (280754), with no fuse at all. Both keep twenty minutes
/// on the clock. That two-second gap is retail's, and it is the difference between the change reading
/// as a stagger and reading as a blink.
/// </para>
/// <para>
/// <b>And on <c>6001</c> they re-pick.</b> The first form takes a random one of whoever is hitting it,
/// the second takes whoever is closest to dying — the same order, answered differently by the two
/// forms, so the group gets harder to peel off a healer as the fight goes on.
/// </para>
/// <para>
/// One class covers both because the shape is identical and only the ids and the fuse differ; which
/// branch set applies is chosen from the owner's npc id at construction.
/// </para>
/// <para>
/// <b>Not translated:</b> the casts on every branch and on entering combat, the <c>say_to_all</c> on
/// <c>6001</c> (no shout row exists for either npc), and <c>on_see_friend_killed_by_user</c>, an event
/// our runtime does not raise — retail uses it to make a subordinate leave when it watches another one
/// die to a player, which is why an add pack thins out rather than fighting to the last.
/// </para>
/// </remarks>
[AIName("hanuman_subordinate")]
public class HanumanSubordinateAI : PatternAi
{
    /// <summary>Retail's messages: change once, change again, and re-pick.</summary>
    public const int ChangeOnce = 5001;
    public const int ChangeAgain = 5002;
    public const int PickAnother = 6001;

    private const int Subordinate2 = 280753;
    private const int Subordinate3 = 280754;

    /// <summary>Retail's <c>SPAWN_ID_1</c> on both forms.</summary>
    private const int Successor = 1;

    /// <summary>Retail's <c>live_time</c> on what either form leaves behind.</summary>
    private const int SuccessorLife = 1200;

    /// <summary>Retail's <c>BTIMERI_INDEX_0</c>, and the fuse the first form puts on its change.</summary>
    private const int Fuse = 0;
    private const int FuseMillis = 2000;

    private static readonly AiPattern FirstForm = new AiPattern
    {
        OnMessage = Of(
            Branch(2, "", [When.Message(ChangeOnce)],
                Do.ArmTimer(Fuse, FuseMillis)),

            Branch(1, "", [When.Message(PickAnother)],
                Do.SwitchTarget(AggroTarget.RANDOM))),

        OnBattleTimer = Of(
            Branch(3, "", [When.Timer(Fuse)],
                Do.SpawnNear(Subordinate2, Successor, count: 1, liveSeconds: SuccessorLife),
                Do.DespawnSelf())),
    };

    private static readonly AiPattern SecondForm = new AiPattern
    {
        OnMessage = Of(
            Branch(2, "", [When.Message(ChangeAgain)],
                Do.SpawnNear(Subordinate3, Successor, count: 1, liveSeconds: SuccessorLife),
                Do.DespawnSelf()),

            Branch(1, "", [When.Message(PickAnother)],
                Do.SwitchTarget(AggroTarget.LOWEST_HP))),
    };

    private readonly AiPattern pattern;

    public HanumanSubordinateAI(Npc owner)
        : base(owner)
    {
        pattern = owner.GetNpcId() == Subordinate2 ? SecondForm : FirstForm;
    }

    protected override AiPattern Pattern => pattern;
}
