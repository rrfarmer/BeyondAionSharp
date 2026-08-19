using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Dark Poeta's three power generators (214895, 214896, 214897). Retail patterns
/// <c>IDLF1_Generator</c>, <c>IDLF1_Gener_02</c> and <c>IDLF1_Gener_03</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>All three ran <c>aggressive</c></b>, so a room whose
/// whole point is that the generator keeps feeding cores down the corridors was three inert machines that
/// stood and hit back.
/// <para>
/// <b>The cores do not appear at the generator.</b> Every summon in all three patterns carries
/// <c>SPAWN_LOCATION_WAY_POINT_START</c> with a named route: the core spawns at the head of a corridor and
/// walks it. The walk is the mechanic — it is the time the group gets to break off and intercept — so a
/// port that dropped the cores at the generator's feet would be describing a different room. That spawn
/// location is new to the pattern engine with this change; 881 spawns across the 5.8 dump use it.
/// </para>
/// <para>
/// <b>The three patterns are the same machine with different wiring</b>, which is why they share a class:
/// identical branch structure, identical thresholds, identical clocks, differing only in which cores each
/// summons and down which corridors. Each generator owns a numbered pair of cores and its own routes.
/// </para>
/// <list type="bullet">
/// <item><b>on engaging</b> — a five-second clock, a twelve-second clock, and a skill on whoever pulled</item>
/// <item><b>below 80</b> — once: two cores, and a self-cast</item>
/// <item><b>below 35</b> — once: three cores, and a self-cast</item>
/// <item><b>the twelve-second clock</b> — a skill on the current target, on a cadence that tightens from
/// fifteen seconds to twelve once the generator is below thirty</item>
/// <item><b>on dying</b> — one last core, by either hand: retail writes <c>on_killed_by_user</c> and
/// <c>on_killed_by_npc</c> as separate branches with byte-identical bodies</item>
/// </list>
/// <para>
/// <b>Not translated.</b> The three skill indices (<c>SKILLI_INDEX_0</c> and <c>_1</c> on the target,
/// <c>_2</c> on self at both thresholds), whose ids are unresolved, and <c>despawn_at_attack_state</c>,
/// which every core spawn carries and which this engine has no concept for. The clocks, the thresholds,
/// the cores, the corridors and the death spawn are all of retail's structure this port can state.
/// </para>
/// </remarks>
[AIName("dark_poeta_generator")]
public class DarkPoetaGeneratorAI : PatternAi
{
    private const int MainGenerator = 214895;
    private const int AuxiliaryGenerator = 214896;

    /// <summary>Retail's <c>BIDLF1_NM_GCore1_50_Ah</c> through <c>_GCore6_</c>, a numbered pair each.</summary>
    private const int LightCore = 281088;
    private const int WaveCore = 281089;
    private const int TorpidityCore = 281090;
    private const int ShockwaveCore = 281091;
    private const int ConfusionCore = 281092;
    private const int SummonsCore = 281093;

    /// <summary>Retail's <c>spawn_range</c>, the same five metres on every core in all three patterns.</summary>
    private const float Scatter = 5f;

    /// <summary>Retail's <c>SPAWN_ID_1</c>, <c>_2</c> and <c>_3</c>: death, the low band, the high band.</summary>
    private const int DeathSpawn = 1;
    private const int LowSpawn = 2;
    private const int HighSpawn = 3;

    /// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> and <c>_BETA_1</c> — each threshold opens once.</summary>
    private const int LowBandOpened = 1;
    private const int HighBandOpened = 2;

    private const int CoreClock = 0;
    private const int SkillClock = 1;

    private static readonly AiPattern MainPattern = Build(
        deathCore: (WaveCore, "NPCPath_GCore_02c"),
        low:
        [
            (LightCore, "NPCPath_GCore_01a", 0),
            (WaveCore, "NPCPath_GCore_02a", 0),
            (WaveCore, "NPCPath_GCore_02b", 30),
        ],
        high:
        [
            (LightCore, "NPCPath_GCore_01a", 0),
            (WaveCore, "NPCPath_GCore_02a", 30),
        ]);

    private static readonly AiPattern AuxiliaryPattern = Build(
        deathCore: (ShockwaveCore, "NPCPath_GCore_04c"),
        low:
        [
            (TorpidityCore, "NPCPath_GCore_03a", 0),
            (ShockwaveCore, "NPCPath_GCore_04a", 0),
            (ShockwaveCore, "NPCPath_GCore_04b", 30),
        ],
        high:
        [
            (TorpidityCore, "NPCPath_GCore_03a", 0),
            (ShockwaveCore, "NPCPath_GCore_04a", 30),
        ]);

    private static readonly AiPattern EmergencyPattern = Build(
        deathCore: (SummonsCore, "NPCPath_GCore_06c"),
        low:
        [
            (ConfusionCore, "NPCPath_GCore_05a", 0),
            (SummonsCore, "NPCPath_GCore_06a", 30),
            (SummonsCore, "NPCPath_GCore_06b", 30),
        ],
        high:
        [
            (ConfusionCore, "NPCPath_GCore_05a", 0),
            (SummonsCore, "NPCPath_GCore_06a", 30),
        ]);

    public DarkPoetaGeneratorAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => GetNpcId() switch
    {
        MainGenerator => MainPattern,
        AuxiliaryGenerator => AuxiliaryPattern,
        _ => EmergencyPattern,
    };

    /// <summary>The shape all three share, given the cores one of them sends.</summary>
    /// <remarks>
    /// <b>Retail's own lifetimes are carried through per core rather than flattened.</b> They are not
    /// uniform even within one generator — the main generator's low band sends two cores that stay and
    /// one that leaves at thirty seconds, while the emergency generator's low band keeps one and times
    /// out two — so a class that assumed one number per band would have been wrong on five of these
    /// fifteen spawns.
    /// </remarks>
    private static AiPattern Build(
        (int NpcId, string Path) deathCore,
        (int NpcId, string Path, int LiveSeconds)[] low,
        (int NpcId, string Path, int LiveSeconds)[] high)
    {
        PatternAction[] lowCores = low
            .Select(c => Do.SpawnOnPath(c.NpcId, LowSpawn, c.Path, Scatter, c.LiveSeconds)).ToArray();
        PatternAction[] highCores = high
            .Select(c => Do.SpawnOnPath(c.NpcId, HighSpawn, c.Path, Scatter, c.LiveSeconds)).ToArray();

        return new AiPattern
        {
            OnEnterAttack = Of(
                Branch(20, "", When.Always,
                    Do.ArmTimer(CoreClock, 5000),
                    Do.ArmTimer(SkillClock, 12000))),

            OnBattleTimer = Of(
                Branch(17, "below 35, opening", [When.Timer(CoreClock), When.HpBelow(35),
                        When.FirstTime(LowBandOpened)],
                    [Do.ArmTimer(CoreClock, 5000), .. lowCores]),

                Branch(16, "below 80, opening", [When.Timer(CoreClock), When.HpBelow(80),
                        When.FirstTime(HighBandOpened)],
                    [Do.ArmTimer(CoreClock, 5000), .. highCores]),

                // The skill clock. Retail's three bands differ in cadence, and the lowest is the tightest.
                Branch(15, "below 30", [When.Timer(SkillClock), When.HpBelow(30)],
                    Do.ArmTimer(SkillClock, 12000)),

                Branch(14, "31-60", [When.Timer(SkillClock), When.HpBetween(31, 60)],
                    Do.ArmTimer(SkillClock, 15000)),

                Branch(13, "61-100", [When.Timer(SkillClock), When.HpBetween(61, 100)],
                    Do.ArmTimer(SkillClock, 15000)),

                // Retail's two heartbeats, so neither clock can stop between bands.
                Branch(12, "", [When.Timer(CoreClock)],
                    Do.ArmTimer(CoreClock, 5000)),

                Branch(11, "", [When.Timer(SkillClock)],
                    Do.ArmTimer(SkillClock, 5000))),

            // on_killed_by_user and on_killed_by_npc, which retail writes twice with the same body.
            OnDie = Of(
                Branch(22, "", When.Always,
                    Do.SpawnOnPath(deathCore.NpcId, DeathSpawn, deathCore.Path, Scatter))),
        };
    }
}
