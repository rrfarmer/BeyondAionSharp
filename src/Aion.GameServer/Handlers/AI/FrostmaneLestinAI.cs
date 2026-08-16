using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Frostmane Lestin (212875). Retail pattern <c>ND2_ElementalSu2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He was on <c>summoner</c>, the generic
/// table-driven Java AI, with a summon table that got three separate things wrong at once — and all
/// three are visible in five lines of data:
/// <code>
///   &lt;percentage percent="80"&gt;&lt;summonGroup npcId="280481" minCount="4" distance="10"/&gt;
///   &lt;percentage percent="60"&gt;&lt;summonGroup npcId="280481" minCount="4" distance="10"/&gt;
///   &lt;percentage percent="40"&gt;&lt;summonGroup npcId="280481" minCount="4" distance="10"/&gt;
/// </code>
/// <list type="bullet">
/// <item><b>The same add three times.</b> Retail escalates through three different elementals —
/// 280489, then 280490, then 280491. We summoned 280481 at every rung, which is a fourth NPC of the
/// same name and a level lower.</item>
/// <item><b>The wrong thresholds.</b> Retail's bands are 66-90, 41-65 and 21-40, so the waves land on
/// dropping below 90, 66 and 41. Ours were 80, 60 and 40.</item>
/// <item><b>They accumulate.</b> Each retail wave <b>despawns the one before it</b>, so four are
/// standing at any time. Ours left all twelve up.</item>
/// </list>
/// <para>
/// The rungs are one-shots and the deepest outranks the rest, so a boss burned down fast goes
/// straight to the third wave. From the second rung on he also rounds on <b>whoever is closest to
/// dying</b> as the wave lands.
/// </para>
/// <para>
/// <b>Not translated.</b> Six skill indices, and with them timers 1 through 4, which carry casts and
/// <c>broadcast_message</c> at 6505-6508 and nothing else. The fourth rung — below 20 — is
/// <em>entirely</em> casts and broadcasts, so it is the one place in the ladder where nothing
/// observable happens; it is kept as a bare re-arm because it consumes the tick it fires on, exactly
/// as Padmarashka's cast-only step does.
/// </para>
/// </remarks>
[AIName("frostmane_lestin")]
public class FrostmaneLestinAI : PatternAi
{
    // BDF2_NM_ElementalAir_Su1/Su2/Su3_40_Ae. Three waves, three different elementals.
    private const int FirstWave = 280489;
    private const int SecondWave = 280490;
    private const int ThirdWave = 280491;

    // Retail's SPAWN_ID_1..3, one per wave, so each can clear the one before it.
    private const int Group1 = 1;
    private const int Group2 = 2;
    private const int Group3 = 3;

    private const int PerWave = 4;
    private const int WaveLife = 600;

    /// <summary>Retail's <c>spawn_range</c>: twelve metres for the first wave, fifteen after.</summary>
    private const float FirstRing = 12f;
    private const float LaterRing = 15f;

    // Retail's ALPHA_1..4, one per rung.
    private const int Below90 = 1;
    private const int Below66 = 2;
    private const int Below41 = 3;
    private const int Below21 = 4;

    private const int OpeningMillis = 10000;
    private const int RungReArmMillis = 9000;
    private const int IdleMillis = 6000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(12, "", When.Always,
                Do.ArmTimer(0, OpeningMillis))),

        OnBattleTimer = Of(
            // Deepest rung first. This one is all casts and broadcasts -- see the remarks -- but it
            // still spends the tick it fires on.
            Branch(8, "below 20", [When.Timer(0), When.HpBelow(21), When.FirstTime(Below21)],
                Do.ArmTimer(0, RungReArmMillis)),

            Branch(6, "21-40", [When.Timer(0), When.HpBetween(21, 40), When.FirstTime(Below41)],
                Do.ArmTimer(0, RungReArmMillis),
                Do.Despawn(Group2),
                Do.SpawnNear(ThirdWave, Group3, count: PerWave, range: LaterRing, liveSeconds: WaveLife),
                Do.SwitchTarget(AggroTarget.LOWEST_HP)),

            Branch(4, "41-65", [When.Timer(0), When.HpBetween(41, 65), When.FirstTime(Below66)],
                Do.ArmTimer(0, RungReArmMillis),
                Do.Despawn(Group1),
                Do.SpawnNear(SecondWave, Group2, count: PerWave, range: LaterRing, liveSeconds: WaveLife),
                Do.SwitchTarget(AggroTarget.LOWEST_HP)),

            Branch(3, "66-90", [When.Timer(0), When.HpBetween(66, 90), When.FirstTime(Below90)],
                Do.ArmTimer(0, RungReArmMillis),
                Do.SpawnNear(FirstWave, Group1, count: PerWave, range: FirstRing, liveSeconds: WaveLife)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, IdleMillis))),

        // Retail's on_leave_attack_state and on_killed_by_user both clear all three groups.
        OnLeaveAttack = Of(
            Branch(10, "", When.Always,
                Do.Despawn(Group1), Do.Despawn(Group2), Do.Despawn(Group3))),

        OnDie = Of(
            Branch(11, "", When.Always,
                Do.Despawn(Group1), Do.Despawn(Group2), Do.Despawn(Group3))),
    };

    public FrostmaneLestinAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
