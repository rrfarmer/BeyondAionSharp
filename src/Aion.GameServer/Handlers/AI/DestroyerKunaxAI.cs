using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Destroyer Kunax, Idgel Dome. Retail pattern <c>IDLDF5_Fortress_Re_Vritra_01</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His whole fight is one deterministic chain: eight
/// skills, ten seconds apart, cycling forever. Each timer branch arms the next slot, and the eighth
/// arms the first again. Ours ran the same skills off probabilities and cooldowns, so the order and
/// spacing were emergent rather than fixed, and the NPC his last step drops on the tank — kunax's
/// wrath (855009) — was spawned by nothing at all.
/// <para>
/// The index mapping is positional against our list, which is only worth relying on because three
/// independent things agree with it: step 7 casts Aether Prison and the pattern spawns an NPC named
/// <em>kunax's wrath</em> on that same step; steps 3 and 4 are the only two cast at
/// <c>OBJI_SELF</c> and land on Cleaving Massacre and Butcher's Sweep, both sweeps that read as
/// centred on the caster; and the list is exactly eight entries against exactly eight indices. The one
/// loose end is step 0, Ide Scale — a self-buff the pattern casts at the current target, which this
/// engine tolerates for buffs.
/// </para>
/// </remarks>
[AIName("destroyer_kunax")]
public class DestroyerKunaxAI : PatternAi
{
    // The eight steps, in the order the chain runs them.
    private const int IdeScale = 21744;             // index 0
    private const int SlaughteringCleave = 21551;   // index 1
    private const int Onslaught = 21552;            // index 2
    private const int CleavingMassacre = 21553;     // index 3, self
    private const int ButchersSweep = 21554;        // index 4, self
    private const int AerialConfinement = 21555;    // index 5
    private const int BloodyCrash = 21556;          // index 6
    private const int AetherPrison = 21558;         // index 7

    private const int KunaxsWrath = 855009;

    /// <summary>The gap-closer he uses at range, driven by intention rather than by the chain.</summary>
    private const int AggressiveShot = 21550;

    private const int StepMillis = 10000;

    /// <summary>One link of the chain: cast, then arm the next slot.</summary>
    private static PatternBranch Step(int slot, PatternAction cast, params PatternAction[] extra)
    {
        var actions = new List<PatternAction> { Do.ArmTimer((slot + 1) % 8, StepMillis), cast };
        actions.AddRange(extra);
        return Branch(7, $"step {slot}", [When.Timer(slot)], actions.ToArray());
    }

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, 6000))),

        OnBattleTimer = Of(
            Step(0, Do.SkillOnTarget(IdeScale)),
            Step(1, Do.SkillOnTarget(SlaughteringCleave)),
            Step(2, Do.SkillOnTarget(Onslaught)),
            Step(3, Do.SkillOnSelf(CleavingMassacre)),
            Step(4, Do.SkillOnSelf(ButchersSweep)),
            Step(5, Do.SkillOnTarget(AerialConfinement)),
            Step(6, Do.SkillOnTarget(BloodyCrash)),
            Step(7, Do.SkillOnTarget(AetherPrison),
                Do.SpawnOnTarget(KunaxsWrath, spawnId: 1))),
    };

    public DestroyerKunaxAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;

    public override AttackIntention ChooseAttackIntention()
    {
        double dist = 0;
        if (GetTarget() != null)
        {
            dist = PositionUtil.GetDistance(GetOwner(), GetTarget()) - GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide()
                - GetTarget().GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        }
        if (dist > 3 && dist <= 30)
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), AggressiveShot, 56, GetTarget()).UseSkill();
            return AttackIntention.SKILL_ATTACK;
        }
        return base.ChooseAttackIntention();
    }
}
