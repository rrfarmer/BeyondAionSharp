using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four middle bosses of Ophidan Bridge — Hakara, Zubala, Visha and Bahapa. Retail pattern
/// <c>BIDF5_U01_Middle_Boss_Fire</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All four ran on plain <c>aggressive</c>. They
/// share one eleven-branch chain across three health bands and differ only in their two signature
/// skills, which the pattern calls "trait 1" and "trait 2" — fire for Zubala, poison for Visha, cold
/// for Bahapa, madness for Hakara.
/// <para>
/// Every band opens with a trait, works through a slash, and closes on a slash flung at a random
/// attacker. Below 70% the middle step becomes the disease pair — Fatal Disease and Boost Deadly
/// Virulency together — and below 40% the opener picks between the two traits on a coin flip.
/// </para>
/// <para>
/// **The index mapping is a rotation by two**, confirmed four separate ways: index 2 is the only
/// ATTACK in every list (Swift Edge, and the branches call it "slash"); indices 0 and 1 are cast
/// together on the branch commented "disease-poison aura" and land on the two disease debuffs;
/// indices 3 and 4 land on each boss's own unique tail, which is what makes them traits; and index 5,
/// self-cast on waking, lands on Midnight Robe, the only BUFF and the only entry our data marks
/// <c>is_post_spawn</c>.
/// </para>
/// <para>
/// **Hakara is one skill short, upstream.** He carries six entries where his three siblings carry
/// seven, so he has a trait 1 and no trait 2 — and the Java reference has the same six, so this is an
/// aionemu data gap rather than a porting error. His trait-2 branches therefore cast nothing: the
/// 41-70 opener, and half the openers below 40%. Everything else about him is faithful, and his
/// npc_skills probabilities still drive ordinary casting, so he is not silent.
/// </para>
/// <para>
/// <b>They are part of Ophidan Bridge's linked pull, at fifty metres rather than thirty</b>, and they
/// answer the call with a <b>million</b> hate points where a fugitive uses ten thousand. A middle boss
/// sent after a player does not come off them for anything.
/// </para>
/// <para>
/// <b>Killing one is what makes the fugitives run.</b> The death branch broadcasts <c>10000</c> — the
/// signal every fugitive grade answers by fleeing — and clears the beritran support combatants around
/// the post. Walking away from the fight clears them too, without the signal.
/// </para>
/// <para>
/// <b>Not translated.</b> <c>set_condition_spawn_variable mboss_die</c>; the <c>10800</c> broadcast,
/// which the check-marker controller answers by placing despawn markers at the <em>other two</em>
/// strongholds; the <c>11100</c> broadcast, whose only listener binds to no npc we spawn; and the
/// support relay (856398) the death branch leaves behind, which re-sends all three messages every six
/// seconds. Each is in the log with what it would take.
/// </para>
/// </remarks>
[AIName("middle_boss_fire")]
public class MiddleBossFireAI : PatternAi
{
    /// <summary>
    /// Retail's <c>range_as_meter</c> on every message these four send — fifty, where the fugitives
    /// and the velkurs call at thirty.
    /// </summary>
    private const float Reach = 50f;

    /// <summary>
    /// Retail's <c>point_to_add</c> when a middle boss answers the call: <b>a million</b>, a hundred
    /// times what a fugitive puts on the same order. Nothing takes a middle boss off the player it was
    /// sent after.
    /// </summary>
    private const int Absolute = 1000000;

    /// <summary><c>IDF5_U1_Vri_Support_Fi_65_Ae1</c> — a beritran support combatant.</summary>
    private const int Support = 231185;

    /// <summary>Retail's <c>bound_radius</c> and <c>max_count</c> on all three sweeps.</summary>
    private const float SupportSweep = 50f;
    private const int SupportEach = 10;

    // Shared by all four, at the same list positions.
    private const int FatalDisease = 21286;          // index 0
    private const int BoostDeadlyVirulency = 17005;  // index 1
    private const int SwiftEdge = 17332;             // index 2 — "slash", the only ATTACK
    private const int MidnightRobe = 20700;          // index 5 — self-cast on waking

    /// <summary>Each boss's two signature skills. Hakara's second is missing from our data.</summary>
    private static readonly Dictionary<int, (int Trait1, int Trait2)> Traits = new()
    {
        [235772] = (17900, 0),      // hakara  — Losing Rationality, and nothing for trait 2
        [235773] = (18176, 20575),  // zubala  — Soaring Flames, Inferno Breath
        [235774] = (20085, 21145),  // visha   — Throw Poison, Diffusive Poison
        [235775] = (16923, 17250),  // bahapa  — Cold Attack, Cold Air Emission
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(1000, "", When.Always, Do.SkillOnSelf(MidnightRobe))),

        OnEnterAttack = Of(
            Branch(1000, "", When.Always,
                Do.ArmTimer(0, 5000),
                Do.Broadcast(OphidanBridgeCallAI.Call, Reach, aboutTarget: true))),

        OnMessage = Of(
            Branch(900, "", [When.Message(OphidanBridgeCallAI.Call)],
                Do.HateMessageTarget(Absolute))),

        // Retail writes this twice, on on_killed_by_user and on_die, behind one test-and-set flag so
        // that only whichever fires first runs. Our runtime raises one death event, which is that flag.
        OnDie = Of(
            Branch(1000, "a stronghold falls", When.Always,
                Do.Broadcast(OphidanBridgeCallAI.Escape, Reach),
                Do.DespawnKind(Support, SupportSweep, SupportEach))),

        OnLeaveAttack = Of(
            Branch(1000, "", When.Always,
                Do.DespawnKind(Support, SupportSweep, SupportEach))),

        OnBattleTimer = Of(
            Branch(1000, "71-100 trait 1", [When.Timer(0), When.HpBetween(71, 100)],
                Do.ArmTimer(1, 6000), Do.Custom(ai => Cast(ai, t => t.Trait1))),
            Branch(995, "71-100 slash", [When.Timer(1), When.HpBetween(71, 100)],
                Do.ArmTimer(2, 9000), Do.SkillOnTarget(SwiftEdge)),
            Branch(980, "71-100 slash at random", [When.Timer(2), When.HpBetween(71, 100)],
                Do.ArmTimer(0, 9000), Do.SkillOn(NpcSkillTargetAttribute.RANDOM, SwiftEdge)),

            Branch(900, "41-70 trait 2", [When.Timer(0), When.HpBetween(41, 70)],
                Do.ArmTimer(1, 6000), Do.Custom(ai => Cast(ai, t => t.Trait2))),
            Branch(890, "41-70 disease pair", [When.Timer(1), When.HpBetween(41, 70)],
                Do.ArmTimer(2, 11500), Do.SkillOnTarget(FatalDisease), Do.SkillOnTarget(BoostDeadlyVirulency)),
            Branch(880, "41-70 slash at random", [When.Timer(2), When.HpBetween(41, 70)],
                Do.ArmTimer(0, 9000), Do.SkillOn(NpcSkillTargetAttribute.RANDOM, SwiftEdge)),

            Branch(800, "0-40 trait 1", [When.Chance(50), When.Timer(0), When.HpBelow(40)],
                Do.ArmTimer(1, 6000), Do.Custom(ai => Cast(ai, t => t.Trait1))),
            Branch(795, "0-40 trait 2", [When.Timer(0), When.HpBelow(40)],
                Do.ArmTimer(1, 6000), Do.Custom(ai => Cast(ai, t => t.Trait2))),
            Branch(790, "0-40 slash twice", [When.Timer(1), When.HpBelow(40)],
                Do.ArmTimer(2, 13000), Do.SkillOnTarget(SwiftEdge), Do.SkillOnTarget(SwiftEdge)),
            Branch(780, "0-40 disease", [When.Timer(2), When.HpBelow(40)],
                Do.ArmTimer(0, 11500), Do.SkillOnTarget(BoostDeadlyVirulency)),

            // Every branch above is banded, so a tick landing between bands would end the chain.
            Branch(1, "", [When.Timer(0)], Do.ArmTimer(0, 5000))),
    };

    /// <summary>
    /// Casts whichever of this boss's two signature skills the branch calls for.
    /// </summary>
    /// <remarks>
    /// The skill differs per NPC on a shared pattern, so it cannot be a constant in the table.
    /// Hakara's second trait is 0 — absent from our data and from Java's — and is skipped rather than
    /// substituted, since guessing a skill would be worse than a branch that does nothing.
    /// </remarks>
    private static void Cast(PatternAi ai, System.Func<(int Trait1, int Trait2), int> pick)
    {
        if (!Traits.TryGetValue(ai.GetOwner().GetNpcId(), out var traits))
            return;
        int skill = pick(traits);
        if (skill != 0)
            ai.CastSkill(skill, NpcSkillTargetAttribute.MOST_HATED);
    }

    public MiddleBossFireAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
