using System.Threading.Tasks;
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
/// Tahabata Pyrelord (215280), Dark Poeta. Retail pattern <c>Dragon_G1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Previously an aionemu class with an enrage timer
/// and two skill hooks and <b>no rotation at all</b>: everything he did between pulling and dying was
/// whatever his npc_skills probabilities rolled. The last version of this file said so and deferred
/// the rebuild; this is that rebuild.
/// <para>
/// His fight is four chained timer slots per health band, plus a fifth chain below 30% that the
/// banded ones never return from, and a ten-minute fuse on slot 9:
/// </para>
/// <list type="bullet">
/// <item><b>81-100</b> — T1→T2→T3→T4→T1, fifteen seconds a step bar the wrap at eleven</item>
/// <item><b>61-80</b> — T0 hands over to a second loop, T5→T6→T7→T8→T5, dropping a ring of four
/// flame centers on the two steps that bracket it</item>
/// <item><b>31-60</b> — T0 hands over to T1→T2→T3→T4→T1; two of those steps put a ring of summon
/// spots out, each of which calls up a faithful subordinate</item>
/// <item><b>below 30</b> — T0 hands over to T5→T6→T7→T8→T5 one last time; the spots it places now
/// call up drakan instead. Note the guards: entry needs below 30, but the chain itself only tests
/// below <b>45</b>, so once it is running it keeps running.</item>
/// <item><b>timer 9</b> — ten minutes after he is pulled, he says nobody is worthy and wipes</item>
/// </list>
/// <para>
/// <b>What this replaces.</b> The old class spawned faithful subordinates (281258) and drakan (281259)
/// directly, hung off the casts of Eruption of Power and Powerful Flame, at eight coordinates of
/// aionemu's own choosing. Retail spawns neither directly. It places <i>summon spots</i> (281262,
/// 281263) at four shared points, and each spot is what calls up the slave — which is why both kinds
/// of slave arrive on the same four marks, and why thinning the spots thins the wave. The flame
/// centers (281261) are the third thing on those timers and were spawned by nothing at all.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Eleven indices are addressed; his npc_skills holds fifteen
/// entries built from nine distinct skills, several of them aionemu chain constructions. Nine cannot
/// answer eleven, so nothing here maps. The chain, the spawns and the target switches are all
/// index-free, so what is translated is faithful and his npc_skills probabilities still drive what he
/// actually casts. The one skill kept by id is the enrage — it is not in his npc_skills at all, which
/// is its own evidence that our list and retail's are different lists.
/// </para>
/// <para>
/// <b>Also not translated:</b> the two <c>say_to_all</c> lines (the string ids are client-side and we
/// have no mapping for them), and the small treasure box retail leaves beside the corpse.
/// </para>
/// </remarks>
[AIName("tahabata_pyrelord")]
public class TahabataPyrelordAI : PatternAi
{
    /// <summary>Retail's battle timer 9, armed on entering combat.</summary>
    private const int EnrageMillis = 600000;

    private const int FlameCenter = 281261;
    private const int CyclopsSpot = 281262;
    private const int DrakanSpot = 281263;

    /// <summary>Left where he falls; retail spawns it from <c>on_killed_by_user</c>.</summary>
    private const int PrimalDragon = 281265;

    /// <summary>"You are unworthy." — the instance wipe, kept from the aionemu class.</summary>
    private const int Unworthy = 19679;

    // Retail's own spawn ids. Dying clears the first three and leaves the fourth standing.
    private const int Flames = 1;
    private const int CyclopsSpots = 2;
    private const int DrakanSpots = 3;
    private const int Corpse = 4;

    private const int MarkerLife = 10;

    /// <summary>
    /// Sent the moment a ring of cyclops spots goes out. Every subordinate still standing from the
    /// previous ring removes itself — see <see cref="TahabataGargoyleAI"/>.
    /// </summary>
    public const int ClearTheOldWave = 3415;
    private const float ClearRange = 50f;

    /// <summary>Pattern <c>dir</c> is degrees; the engine's own converter turns it into a heading.</summary>
    private static sbyte Facing(int degrees) =>
        (sbyte)PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    /// <summary>
    /// The four flame points, shared with Vanuka Infernus — the two dragons fight in the same arena.
    /// </summary>
    private static readonly SpawnSpot[] FlamePoints =
    [
        new SpawnSpot(1177f, 1241f, 143.322f, Facing(-28)),
        new SpawnSpot(1173f, 1231f, 144.788f, Facing(126)),
        new SpawnSpot(1187f, 1229f, 143.8f, Facing(-138)),
        new SpawnSpot(1190f, 1238f, 142.651f, Facing(-59)),
    ];

    /// <summary>The four marks a summon spot lands on. Both kinds of spot use the same four.</summary>
    private static readonly SpawnSpot[] SummonPoints =
    [
        new SpawnSpot(1192f, 1254f, 139.917f, Facing(-28)),
        new SpawnSpot(1169f, 1246f, 143.041f, Facing(73)),
        new SpawnSpot(1173f, 1217f, 145.415f, Facing(178)),
        new SpawnSpot(1198f, 1224f, 143.119f, Facing(-83)),
    ];

    private static readonly PatternAction Ring =
        Do.SpawnAt(FlameCenter, Flames, MarkerLife, FlamePoints);

    /// <summary>A ring of cyclops spots, and the call that clears whatever the last ring left.</summary>
    private static readonly PatternAction[] CallCyclops =
    [
        Do.Broadcast(ClearTheOldWave, ClearRange),
        Do.SpawnAt(CyclopsSpot, CyclopsSpots, MarkerLife, SummonPoints),
    ];

    private static readonly PatternAction CallDrakan =
        Do.SpawnAt(DrakanSpot, DrakanSpots, MarkerLife, SummonPoints);

    private static readonly PatternAction Scatter = Do.SwitchTarget(AggroTarget.RANDOM);

    /// <summary>Announces the S-rank clock, which retail sends beside its own shout.</summary>
    private static readonly PatternAction SayTheClockStarted = Do.Custom(ai =>
        PacketSendUtility.BroadcastToMap(ai.GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_TIME()));

    private static readonly PatternAction SayTimeIsUp = Do.Custom(ai =>
        PacketSendUtility.BroadcastToMap(ai.GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_END()));

    /// <summary>
    /// Retail casts the wipe and despawns in the same breath; this waits for the cast to land and then
    /// removes him, which is what the aionemu class did and what players actually see.
    /// </summary>
    private static readonly PatternAction Wipe = Do.Custom(ai =>
    {
        if (!ai.IsDead())
            ai.GetOwner().QueueSkill(Unworthy, 50, 3000);
    });

    /// <summary>One link of a chain: arm the next slot, then whatever else the branch does.</summary>
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
                Do.ArmTimer(1, 9000),
                Do.ArmTimer(0, 11000),
                Do.ArmTimer(9, EnrageMillis),
                SayTheClockStarted)),

        OnBattleTimer = Of(
            // The fuse. It outranks every band, so it lands whatever else was due.
            Branch(22, "time is up", [When.Timer(9)],
                SayTimeIsUp,
                Wipe),

            // Below 45 — entered from T0 below 30 and never left.
            Step(20, [When.Timer(8), When.HpBelow(45)], next: 5, delay: 9000, Scatter),
            Step(19, [When.Timer(6), When.HpBelow(45)], next: 7, delay: 12000, Ring),
            Step(18, [When.Timer(7), When.HpBelow(45)], next: 8, delay: 18000, CallDrakan, Scatter),
            Step(17, [When.Timer(5), When.HpBelow(45)], next: 6, delay: 12000, Scatter),
            Step(16, [When.Timer(0), When.HpBelow(30), When.FirstTime(3)], next: 5, delay: 8000, Scatter),

            // 31-60.
            Step(15, [When.HpBetween(31, 60), When.Timer(4)], next: 1, delay: 12000, CallCyclops),
            Step(14, [When.HpBetween(31, 60), When.Timer(3)], next: 4, delay: 16000, Scatter),
            Step(13, [When.HpBetween(31, 60), When.Timer(2)], next: 3, delay: 8000, Ring),
            Step(12, [When.HpBetween(31, 60), When.Timer(1)], next: 2, delay: 13000),
            Branch(11, "", [When.HpBetween(31, 60), When.Timer(0), When.FirstTime(2)],
                [Do.ArmTimer(0, 9000), Do.ArmTimer(1, 12000), .. CallCyclops]),

            // 61-80.
            Step(10, [When.HpBetween(61, 80), When.Timer(8)], next: 5, delay: 10000, Ring),
            Step(9, [When.HpBetween(61, 80), When.Timer(7)], next: 8, delay: 15000),
            Step(8, [When.HpBetween(61, 80), When.Timer(6)], next: 7, delay: 16000, Scatter),
            Step(7, [When.HpBetween(61, 80), When.Timer(5)], next: 6, delay: 15000),
            Branch(6, "", [When.Timer(0), When.HpBetween(61, 80), When.FirstTime(1)],
                Do.ArmTimer(0, 7000),
                Do.ArmTimer(5, 10000),
                Ring),

            // 81-100.
            Step(5, [When.HpBetween(81, 100), When.Timer(4)], next: 1, delay: 11000),
            Step(4, [When.Timer(3), When.HpBetween(81, 100)], next: 4, delay: 15000),
            Step(3, [When.Timer(2), When.HpBetween(81, 100)], next: 3, delay: 15000, Scatter),
            Step(2, [When.Timer(1), When.HpBetween(81, 100)], next: 2, delay: 15000),

            // The heartbeat. Every banded T0 branch above is guarded, so without this a tick that
            // landed between bands would end the T0 chain for the rest of the fight.
            Step(1, [When.Timer(0)], next: 0, delay: 6000)),

        // Retail's on_killed_by_user. The markers go with him; the primal dragon is left standing.
        OnDie = Of(
            Branch(23, "", When.Always,
                Do.Despawn(Flames),
                Do.Despawn(CyclopsSpots),
                Do.Despawn(DrakanSpots),
                Do.SpawnNear(PrimalDragon, Corpse, count: 1, range: 0f))),
    };

    public TahabataPyrelordAI(Npc owner)
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
