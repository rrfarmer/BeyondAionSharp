using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tiamat's four incarnations in Dragon Lord's Refuge — Fissurefang, Graviwing, Petriscale and
/// Wrathclaw.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Patterns <c>IDTiamat_T1_Crack_Key_Named_60_Al</c>,
/// <c>..._Gravity_...</c>, <c>..._Crystal_...</c> and <c>..._Rage_...</c>. The first three are the
/// same fight with a different element: a 9s attack that drops a hazard on the tank, a slower area
/// attack that drops one on everybody, and a bind on a random player once past 30% health.
/// <para>
/// Wrathclaw is the odd one and had <b>no AI at all</b> — his template pointed at plain
/// <c>aggressive</c> while his three siblings shared this class. He places a sphere of wrath and a
/// sphere of peace when he spawns, and every area attack clears them and puts them out again,
/// sometimes with the two swapped between the same two points. That is the whole mechanic: players
/// have to find the right sphere, and where it is keeps changing.
/// </para>
/// <para>
/// What this replaces was invented: two hazards placed on two random players within 30m, every 30s,
/// starting 20s after activation and continuing whether or not anyone was in combat. Retail places
/// them on the actual targets of the two attacks that create them, and the second one lands on the
/// whole raid rather than on two people.
/// </para>
/// <para>
/// The hard-mode twins (236278/236279/236281 and the 856xxx set) bind to their own
/// <c>IDTiamat_Hard_*</c> patterns, which are not translated here; they keep the behaviour they had.
/// </para>
/// </remarks>
[AIName("tiamats_incarnation")]
public class TiamatsIncarnationAI : PatternAi
{
    private const int Fissurefang = 219365;
    private const int Graviwing = 219366;
    private const int Petriscale = 219368;
    private const int Wrathclaw = 219367;

    // Skill indices, corroborated by stack name rather than by position in our list -- which is a
    // different order: our entries run 20105, 20145, 20146, breath, while the pattern's indices are
    // 0 PowerAtk, 1 AreaAtk, 2 HandBind, 3 the breath.
    private const int PowerAtk = 20145;   // Smash, LDF4B_TIAMATAVATAR_POWERATK
    private const int AreaAtk = 20146;    // Incarnate Surge, LDF4B_TIAMATAVATAR_AREAATK
    private const int HandBind = 20105;   // Bite, LDF4B_TIAMATAVATAR_HANDBIND

    private const int CavityOfEarth = 282735;
    private const int GravityWhirlpool = 282727;
    private const int PetrificationCrystal = 282731;

    /// <summary>The two effects every incarnation leaves behind when it dies, at the crack it closes.</summary>
    private static readonly SpawnSpot BurrowSpot = new SpawnSpot(478.4f, 514.2f, 418f);
    private static readonly SpawnSpot EffectSpot = new SpawnSpot(480.5f, 513.9f, 418f);
    private const int BurrowingAttack = 283060;

    private const int SphereOfWrath = 282979;
    private const int SphereOfPeace = 282733;

    /// <summary>The two points his spheres occupy. Which sphere is at which is what changes.</summary>
    private static readonly SpawnSpot NorthPoint = new SpawnSpot(214f, 858f, 246.5f);
    private static readonly SpawnSpot SouthPoint = new SpawnSpot(185f, 838f, 246.5f);

    /// <summary>Combat adds. Retail files these under one id and clears it when the fight ends.</summary>
    private const int Hazards = 1;

    /// <summary>
    /// The death effects, which retail files under the same id it despawns in that same branch.
    /// </summary>
    /// <remarks>
    /// Taken literally, that branch deletes the two effects one line after creating them and the death
    /// is silent. Giving them their own id lets both halves do something: the fight's hazards are
    /// cleared, and the effects play out the six seconds of life the pattern gives them.
    /// </remarks>
    private const int DeathEffects = 2;

    /// <remarks>
    /// Index 3 -- the breath, 20169 / 20157 / 20161 -- is not here: its branch fires on
    /// <c>on_message</c> 71 from Tiamat rather than on a timer, and that message chain is not
    /// translated. The skill keeps its npc_skills probability so it still appears in the fight.
    /// </remarks>
    private static AiPattern Incarnation(int areaAtkRearm, int handBindRearm,
        int deathEffect, PatternAction powerAtkHazard, PatternAction areaAtkHazard)
        => new AiPattern
        {
            OnEnterAttack = Of(
                Branch(5, "SetTimer", When.Always,
                    Do.ArmTimer(0, 3000),
                    Do.ArmTimer(1, 15000),
                    Do.ArmTimer(2, 20000))),

            OnBattleTimer = Of(
                Branch(4, "PowerAtk", [When.Timer(0)],
                    Do.ArmTimer(0, 9000),
                    Do.SkillOnTarget(PowerAtk),
                    powerAtkHazard),

                Branch(3, "AreaAtk", [When.Timer(1)],
                    Do.ArmTimer(1, areaAtkRearm),
                    Do.SkillOnSelf(AreaAtk),
                    areaAtkHazard),

                Branch(2, "HandBind", [When.Timer(2), When.HpBelow(30)],
                    Do.ArmTimer(2, handBindRearm),
                    Do.SkillOn(NpcSkillTargetAttribute.RANDOM, HandBind)),

                // Until it drops past 30% the bind branch never matches, and without this the chain
                // would end on its first tick.
                Branch(1, "Repeat_Handbind", [When.Timer(2)],
                    Do.ArmTimer(2, 3000))),

            OnDie = Of(
                // Retail's order, kept: the despawn comes after the spawns. That is what makes the
                // separate id for the effects load-bearing rather than decorative.
                Branch(6, "Int+1", When.Always,
                    Do.SpawnAt(BurrowingAttack, DeathEffects, liveSeconds: 6, BurrowSpot),
                    Do.SpawnAt(deathEffect, DeathEffects, liveSeconds: 6, EffectSpot),
                    Do.Despawn(Hazards))),
        };

    /// <summary>Puts the two spheres out, wrath at whichever point is named first.</summary>
    private static PatternAction[] PlaceSpheres(SpawnSpot wrathAt, SpawnSpot peaceAt) =>
    [
        Do.SpawnAt(SphereOfWrath, Hazards, liveSeconds: 0, wrathAt),
        Do.SpawnAt(SphereOfPeace, Hazards, liveSeconds: 0, peaceAt),
    ];

    /// <summary>One of his two area attacks: clear the spheres, cast, put them back as given.</summary>
    private static PatternBranch AreaAttack(int priority, string comment, PatternCondition[] conditions,
        SpawnSpot wrathAt, SpawnSpot peaceAt)
    {
        PatternAction[] actions =
        [
            Do.ArmTimer(1, 25000),
            Do.Despawn(Hazards),
            Do.SkillOnSelf(AreaAtk),
            .. PlaceSpheres(wrathAt, peaceAt),
        ];
        return Branch(priority, comment, conditions, actions);
    }

    private static readonly AiPattern WrathclawPattern = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "SpawnCircle", When.Always, PlaceSpheres(NorthPoint, SouthPoint))),

        OnEnterAttack = Of(
            Branch(6, "SetTimer", When.Always,
                Do.ArmTimer(0, 3000),
                Do.ArmTimer(1, 15000),
                Do.ArmTimer(2, 20000))),

        OnBattleTimer = Of(
            Branch(5, "PowerAtk", [When.Timer(0)],
                Do.ArmTimer(0, 9000),
                Do.SkillOnTarget(PowerAtk)),

            // A third of the time the spheres go back where they were...
            AreaAttack(4, "AreaAtk_34", [When.Chance(34), When.Timer(1)], NorthPoint, SouthPoint),

            // ...and otherwise they come back swapped, which is the point of the fight.
            AreaAttack(3, "AreaAtk_100", [When.Timer(1)], SouthPoint, NorthPoint),

            Branch(2, "HandBind", [When.Timer(2), When.HpBelow(30)],
                Do.ArmTimer(2, 30000),
                Do.SkillOn(NpcSkillTargetAttribute.RANDOM, HandBind)),

            Branch(1, "Repeat_Handbind", [When.Timer(2)],
                Do.ArmTimer(2, 3000))),

        OnDie = Of(
            Branch(8, "Int+1", When.Always,
                Do.SpawnAt(BurrowingAttack, DeathEffects, liveSeconds: 6, BurrowSpot),
                Do.SpawnAt(283066, DeathEffects, liveSeconds: 6, EffectSpot),
                Do.Despawn(Hazards))),
    };

    private static readonly Dictionary<int, AiPattern> Tables = new Dictionary<int, AiPattern>
    {
        [Wrathclaw] = WrathclawPattern,

        // Fissurefang's hazard lands under the tank; retail also has it engage its target on arrival,
        // which we leave to the add's own aggressive AI.
        [Fissurefang] = Incarnation(areaAtkRearm: 25000, handBindRearm: 30000,
            deathEffect: 283063,
            powerAtkHazard: Do.SpawnOnTarget(CavityOfEarth, Hazards, range: 1f, liveSeconds: 7),
            areaAtkHazard: Do.SpawnOnEachTarget(CavityOfEarth, Hazards, validDistance: 100f,
                maxTargets: 3, liveSeconds: 25)),

        // Graviwing's lands on a random attacker instead, and does not live as long.
        [Graviwing] = Incarnation(areaAtkRearm: 30000, handBindRearm: 35000,
            deathEffect: 283065,
            powerAtkHazard: Do.SpawnOnAttacker(AggroTarget.RANDOM, GravityWhirlpool, Hazards,
                range: 1f, liveSeconds: 4),
            // Only the most-hated gets one, and it is the widest and shortest-lived of the three.
            areaAtkHazard: Do.SpawnOnEachTarget(GravityWhirlpool, Hazards, validDistance: 100f,
                maxTargets: 1, range: 6f, liveSeconds: 12)),

        // Petriscale's power attack is already raid-wide, so both of its timers drop on everyone.
        [Petriscale] = Incarnation(areaAtkRearm: 25000, handBindRearm: 30000,
            deathEffect: 283064,
            powerAtkHazard: Do.SpawnOnEachTarget(PetrificationCrystal, Hazards, validDistance: 50f,
                maxTargets: 2, liveSeconds: 20),
            areaAtkHazard: Do.SpawnOnEachTarget(PetrificationCrystal, Hazards, validDistance: 100f,
                maxTargets: 3, range: 1f, liveSeconds: 20)),
    };

    private static readonly AiPattern Untranslated = new AiPattern();

    public TiamatsIncarnationAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern =>
        Tables.TryGetValue(GetNpcId(), out AiPattern? table) ? table : Untranslated;

    private bool IsTranslated => Tables.ContainsKey(GetNpcId());

    protected override void HandleActivate()
    {
        base.HandleActivate();
        if (!IsTranslated)
            ScheduleSummons(20000);
    }

    /// <summary>The invented summon cycle, still driving the hard-mode twins.</summary>
    private void ScheduleSummons(int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetTarget() != null)
            {
                List<Player> nearbyPlayers = GetNearbyPlayers();
                if (nearbyPlayers.Count > 1)
                {
                    Player first = RemoveAt(nearbyPlayers, Rnd.NextInt(nearbyPlayers.Count));
                    Player second = RemoveAt(nearbyPlayers, Rnd.NextInt(nearbyPlayers.Count));
                    int summonId = Rnd.Get(GetSummonNpcIds().ToArray());
                    Spawn(summonId, first.GetX(), first.GetY(), first.GetZ(), (sbyte)0);
                    Spawn(summonId, second.GetX(), second.GetY(), second.GetZ(), (sbyte)0);
                    ScheduleSummons(30000);
                }
            }
            return ValueTask.CompletedTask;
        }, delay);
    }

    private static Player RemoveAt(List<Player> list, int index)
    {
        Player p = list[index];
        list.RemoveAt(index);
        return p;
    }

    private List<Player> GetNearbyPlayers()
    {
        return GetKnownList().StreamPlayers().Where(player => !player.IsDead() && IsInRange(player, 30)).ToList();
    }

    protected override void HandleDespawned()
    {
        foreach (int id in GetSummonNpcIds())
            GetPosition().GetWorldMapInstance().GetNpcs(id).ToList().ForEach(npc => npc.GetController().Delete());
        base.HandleDespawned();
    }

    private List<int> GetSummonNpcIds()
    {
        switch (GetNpcId())
        {
            case Graviwing:
                return new List<int> { GravityWhirlpool, 282729 }; // Gravity Whirlpool, Thunderbolt Whirlpool
            case 236279: // Graviwing HM
                return new List<int> { 856074, 856076 };
            case Petriscale:
                return new List<int> { PetrificationCrystal }; // Petrification Crystal
            case 236281: // Petriscale HM
                return new List<int> { 856072 };
            case Fissurefang:
                return new List<int> { CavityOfEarth, 282737 }; // Cavity of Earth, Collapsing Earth
            case 236278: // Fissurefang HM
                return new List<int> { 856068, 856070 };
            default:
                return new List<int>();
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.ALLOW_DECAY:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
