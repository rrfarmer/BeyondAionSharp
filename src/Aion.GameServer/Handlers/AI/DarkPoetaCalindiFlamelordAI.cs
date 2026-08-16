using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Calindi Flamelord (215281), Dark Poeta. Retail pattern <c>Dragon_G2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Tahabata's twin, in the same arena and on the same
/// marks, and the class had the same two faults: <b>no rotation</b>, and an enrage armed on
/// <c>HandleSpawned</c> where retail arms it in <c>on_enter_attack_state</c> — so a group that spent
/// four minutes reaching her arrived with six.
/// <list type="bullet">
/// <item><b>81-100</b> — T1 fires once and the loop settles into T2→T3→T4→T2</item>
/// <item><b>61-80</b> — T0 hands over to T5→T6→T7→T8→T5, ringing the arena with four flame
/// centers on the two steps that bracket it</item>
/// <item><b>31-60</b> — T0 hands over to T1→T2→T3→T4→T1, putting four worm spots out twice a loop</item>
/// <item><b>below 30</b> — T0 hands over to T5→T6→T7→T8→T5 for good, and what she places now is
/// <b>two</b> drakan spots rather than four of anything</item>
/// <item><b>timer 9</b> — ten minutes after she is pulled, the A-rank clock runs out and she wipes</item>
/// </list>
/// <para>
/// <b>Where she differs from Tahabata, and it is not cosmetic.</b> His low chain places four; hers
/// places two, on the first and third marks only. She scatters onto her <i>second</i> most hated on
/// one step where he takes a random one. And she leaves no primal dragon behind — retail's
/// <c>on_killed_by_user</c> clears her markers and drops a treasure box, nothing else.
/// </para>
/// <para>
/// <b>Two calls, not one.</b> 3413 goes out with every ring of worm spots and 3412 with every pair of
/// drakan spots, each clearing the wave of its own kind — so crossing from the worm band into the
/// drakan band does not leave the worms standing. See <see cref="CalindiSlaveAI"/>.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Nine indices are addressed against eight distinct skills, so
/// nothing maps; the enrage is kept by id from the aionemu class, as it is for Tahabata, and her
/// npc_skills probabilities still drive what she actually casts. The <c>say_to_all</c> lines are
/// client string ids we have no mapping for, and the treasure box is left to the instance.
/// </para>
/// </remarks>
[AIName("calindi_flamelord")]
public class DarkPoetaCalindiFlamelordAI : PatternAi
{
    private const int EnrageMillis = 600000;

    private const int FlameCenter = 281270;
    private const int WormSpot = 281271;
    private const int DrakanSpot = 281272;

    /// <summary>"You are unworthy." — the instance wipe, kept from the aionemu class.</summary>
    private const int Unworthy = 19679;

    // Retail's own spawn ids; dying clears all three.
    private const int Flames = 1;
    private const int WormSpots = 2;
    private const int DrakanSpots = 3;

    private const int MarkerLife = 10;

    /// <summary>Sent with every pair of drakan spots. Standing drakan leave on hearing it.</summary>
    public const int ClearTheDrakan = 3412;

    /// <summary>Sent with every ring of worm spots. Standing worms leave on hearing it.</summary>
    public const int ClearTheWorms = 3413;

    private const float CallRange = 50f;

    private static sbyte Facing(int degrees) =>
        (sbyte)PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    /// <summary>The four flame points, shared with Tahabata and Vanuka — one arena, one set of marks.</summary>
    private static readonly SpawnSpot[] FlamePoints =
    [
        new SpawnSpot(1177f, 1241f, 143.322f, Facing(-28)),
        new SpawnSpot(1173f, 1231f, 144.788f, Facing(126)),
        new SpawnSpot(1187f, 1229f, 143.8f, Facing(-138)),
        new SpawnSpot(1190f, 1238f, 142.651f, Facing(-59)),
    ];

    private static readonly SpawnSpot NorthEast = new SpawnSpot(1192f, 1254f, 139.917f, Facing(-28));
    private static readonly SpawnSpot NorthWest = new SpawnSpot(1169f, 1246f, 143.041f, Facing(73));
    private static readonly SpawnSpot SouthWest = new SpawnSpot(1173f, 1217f, 145.415f, Facing(178));
    private static readonly SpawnSpot SouthEast = new SpawnSpot(1198f, 1224f, 143.119f, Facing(-83));

    private static readonly PatternAction Ring =
        Do.SpawnAt(FlameCenter, Flames, MarkerLife, FlamePoints);

    private static readonly PatternAction[] CallWorms =
    [
        Do.Broadcast(ClearTheWorms, CallRange),
        Do.SpawnAt(WormSpot, WormSpots, MarkerLife, NorthEast, NorthWest, SouthWest, SouthEast),
    ];

    /// <summary>Two marks, not four — the first and the third.</summary>
    private static readonly PatternAction[] CallDrakan =
    [
        Do.Broadcast(ClearTheDrakan, CallRange),
        Do.SpawnAt(DrakanSpot, DrakanSpots, MarkerLife, NorthEast, SouthWest),
    ];

    private static readonly PatternAction Scatter = Do.SwitchTarget(AggroTarget.RANDOM);
    private static readonly PatternAction TurnOnTheSecond = Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED);

    private static readonly PatternAction SayTheClockStarted = Do.Custom(ai =>
        PacketSendUtility.BroadcastToMap(ai.GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_A_RANK_BATTLE_TIME()));

    private static readonly PatternAction SayTimeIsUp = Do.Custom(ai =>
        PacketSendUtility.BroadcastToMap(ai.GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_A_RANK_BATTLE_END()));

    private static readonly PatternAction Wipe = Do.Custom(ai =>
    {
        if (!ai.IsDead())
            ai.GetOwner().QueueSkill(Unworthy, 50, 3000);
    });

    private static PatternBranch Step(int priority, PatternCondition[] guards, int next, int delay,
        params PatternAction[] extra)
    {
        PatternAction[] actions = new PatternAction[extra.Length + 1];
        actions[0] = Do.ArmTimer(next, delay);
        extra.CopyTo(actions, 1);
        return Branch(priority, "", guards, actions);
    }

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(21, "", When.Always,
                Do.ArmTimer(0, 6000),
                Do.ArmTimer(1, 8000),
                Do.ArmTimer(9, EnrageMillis),
                SayTheClockStarted)),

        OnBattleTimer = Of(
            Branch(22, "time is up", [When.Timer(9)],
                SayTimeIsUp,
                Wipe),

            // Below 45 — entered from T0 below 30 and never left.
            Step(20, [When.Timer(8), When.HpBelow(45)], next: 5, delay: 7000),
            Step(19, [When.Timer(7), When.HpBelow(45)], next: 8, delay: 12000),
            Branch(18, "", [When.Timer(6), When.HpBelow(45)],
                [Do.ArmTimer(7, 17000), .. CallDrakan]),
            Step(17, [When.Timer(5), When.HpBelow(45)], next: 6, delay: 17000, TurnOnTheSecond),
            Step(16, [When.Timer(0), When.HpBelow(30), When.FirstTime(3)], next: 5, delay: 7000),

            // 31-60.
            Branch(15, "", [When.Timer(4), When.HpBetween(31, 60)],
                [Do.ArmTimer(1, 13000), .. CallWorms]),
            Step(14, [When.Timer(3), When.HpBetween(31, 60)], next: 4, delay: 17000),
            Step(13, [When.Timer(2), When.HpBetween(31, 60)], next: 3, delay: 17000),
            Step(12, [When.Timer(1), When.HpBetween(31, 60)], next: 2, delay: 15000, Scatter),
            Branch(11, "", [When.Timer(0), When.HpBetween(31, 60), When.FirstTime(2)],
                [Do.ArmTimer(0, 7000), Do.ArmTimer(1, 12000), .. CallWorms]),

            // 61-80.
            Step(10, [When.HpBetween(61, 80), When.Timer(8)], next: 5, delay: 12000, Ring),
            Step(9, [When.HpBetween(61, 80), When.Timer(7)], next: 8, delay: 18000),
            Step(8, [When.Timer(6), When.HpBetween(61, 80)], next: 7, delay: 18000),
            Step(7, [When.Timer(5), When.HpBetween(61, 80)], next: 6, delay: 18000, Scatter),
            Branch(6, "", [When.Timer(0), When.HpBetween(61, 80), When.FirstTime(1)],
                Do.ArmTimer(0, 7000),
                Do.ArmTimer(5, 12000),
                Ring),

            // 81-100. Note the wrap: T4 arms T2, so T1 only ever fires the once.
            Step(5, [When.Timer(4), When.HpBetween(81, 100)], next: 2, delay: 18000, Scatter),
            Step(4, [When.Timer(3), When.HpBetween(81, 100)], next: 4, delay: 7000),
            Step(3, [When.Timer(2), When.HpBetween(81, 100)], next: 3, delay: 18000),
            Step(2, [When.Timer(1), When.HpBetween(81, 100)], next: 2, delay: 18000),

            Step(1, [When.Timer(0)], next: 0, delay: 6000)),

        OnDie = Of(
            Branch(23, "", When.Always,
                Do.Despawn(Flames),
                Do.Despawn(WormSpots),
                Do.Despawn(DrakanSpots))),
    };

    public DarkPoetaCalindiFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == Unworthy)
            AIActions.DeleteOwner(this);
    }
}
