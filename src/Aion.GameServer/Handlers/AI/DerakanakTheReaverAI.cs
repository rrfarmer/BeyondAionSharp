using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Derakanak the Reaver (233258), Sauro Supply Base. Retail pattern <c>IDVritra_Base_Drake_Nmd</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO boss with an eighteen-branch rotation that
/// ran on plain <c>aggressive</c>: no adds, so the missing-adds sweep could never see him — the whole
/// fight was simply absent, and he auto-attacked with whatever his skill probabilities rolled.
/// <para>
/// Three regimes, each its own timer chain, entered by one-shot branches on the heartbeat:
/// </para>
/// <list type="bullet">
/// <item><b>81-100</b> — T1 → T2 → T3 → T4 → T1, ten seconds a step</item>
/// <item><b>below 80</b> — announces itself with the fear pair, then T5 → T6 → T7 → T8 → T9 → T5</item>
/// <item><b>below 40</b> — the fear pair again, then T10 → T11 → T12 → T13 → T14, whose last step
/// alternates between looping back to T10 with another fear pair and hopping back to T11</item>
/// </list>
/// <para>
/// <b>Skill indices.</b> Seven indices against a twelve-entry list, and the branch comments name five
/// of them outright: 화염 is <c>Flame</c>, 불꽃뿜기 is <c>Flame Spurt</c>, 축복의 저주 is
/// <c>Curse of Blessing</c>, 공포발산 is <c>Fear Casting</c> and 공황유발 is <c>Fearful Panic</c> —
/// exact name matches, one each. That leaves 마법구 ("magic orb") for <c>Large Magic Missile</c> and
/// 강력한 화염 ("powerful flame") for <c>Fireball</c>, the only other flame debuff and the stronger of
/// the pair. The five unaddressed skills stay on their npc_skills probabilities, which is what retail
/// does with them too.
/// </para>
/// <para>
/// <b>One comment disagrees with its own branch.</b> Step 5 is commented 마법구 but casts index 2,
/// Flame Spurt, where its three sibling 마법구 steps all cast index 0. The action is what runs, so the
/// action is what is reproduced; the comment looks like a copy-paste from the step above it.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing. Every branch, every cast and every target of this pattern is here.
/// </para>
/// </remarks>
[AIName("derakanak_the_reaver")]
public class DerakanakTheReaverAI : PatternAi
{
    private const int LargeMagicMissile = 16987;  // index 0 — 마법구
    private const int Flame = 16574;              // index 1 — 화염
    private const int FlameSpurt = 16918;         // index 2 — 불꽃뿜기
    private const int Fireball = 16919;           // index 3 — 강력한 화염
    private const int FearCasting = 17888;        // index 4 — 공포발산
    private const int CurseOfBlessing = 16702;    // index 5 — 축복의 저주
    private const int FearfulPanic = 20782;       // index 6 — 공황유발

    private const int PhaseTwo = 1;      // FLAGVARI_ALPHA_1
    private const int PhaseThree = 2;    // FLAGVARI_ALPHA_2
    private const int AlternateTail = 3; // FLAGVARI_ALPHA_3

    /// <summary>Both phase changes open the same way.</summary>
    private static readonly PatternAction[] FearPair =
    [
        Do.SkillOnTarget(FearCasting),
        Do.SkillOnTarget(FearfulPanic),
    ];

    /// <summary>A plain link: arm the next slot and cast one thing at the current target.</summary>
    private static PatternBranch Step(int priority, string comment, PatternCondition[] guards,
        int next, int delay, int skill)
        => Branch(priority, comment, guards, Do.ArmTimer(next, delay), Do.SkillOnTarget(skill));

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(1, "1. combat starts > magic orb", When.Always,
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(1, 10000),
                Do.SkillOnTarget(LargeMagicMissile))),

        OnBattleTimer = Of(
            // --- below 40: T10 -> T11 -> T12 -> T13 -> T14 ------------------------------------------
            // T14 alternates through a flag: one pass loops the whole way back to T10 and re-casts the
            // fear pair, the next hops straight back to T11 for another fireball.
            Branch(19, "17-2. switch target / fear casting / fearful panic",
                [When.Timer(14), When.Consuming(AlternateTail)],
                Do.ArmTimer(10, 15000),
                Do.SwitchTarget(AggroTarget.RANDOM),
                FearPair[0],
                FearPair[1]),

            Step(18, "17-1. fireball", [When.Timer(14), When.FirstTime(AlternateTail)],
                next: 11, delay: 10000, skill: Fireball),

            Step(17, "16. flame spurt", [When.Timer(13)], next: 14, delay: 10000, skill: FlameSpurt),

            Branch(16, "15. switch target / fireball", [When.Timer(12)],
                Do.ArmTimer(13, 11000),
                Do.SkillOnTarget(Fireball),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(Fireball)),

            Step(15, "14. flame spurt", [When.Timer(11)], next: 12, delay: 10000, skill: FlameSpurt),

            // The only branch that reaches past the top of the hate list.
            Branch(14, "13. curse of blessing", [When.Timer(10)],
                Do.ArmTimer(11, 12000),
                Do.SkillOnTarget(CurseOfBlessing),
                Do.SkillOn(NpcSkillTargetAttribute.SECOND_MOST_HATED, CurseOfBlessing)),

            // Phase three. Note it does not re-arm timer 0: the heartbeat stops here, and the chain
            // below carries the rest of the fight on its own.
            Branch(13, "12. phase three > fear casting / fearful panic",
                [When.Timer(0), When.HpBelow(40), When.FirstTime(PhaseThree)],
                Do.ArmTimer(10, 15000),
                FearPair[0],
                FearPair[1]),

            // --- 41-80: T5 -> T6 -> T7 -> T8 -> T9 -> T5 --------------------------------------------
            Step(12, "11. flame spurt", [When.Timer(9), When.HpBetween(41, 80)],
                next: 5, delay: 10000, skill: FlameSpurt),

            Step(11, "10. magic orb", [When.Timer(8), When.HpBetween(41, 80)],
                next: 9, delay: 10000, skill: LargeMagicMissile),

            Branch(10, "9. flame spurt / switch target / flame",
                [When.Timer(7), When.HpBetween(41, 80)],
                Do.ArmTimer(8, 12000),
                Do.SkillOnTarget(FlameSpurt),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(Flame)),

            Step(9, "8. magic orb", [When.Timer(6), When.HpBetween(41, 80)],
                next: 7, delay: 10000, skill: LargeMagicMissile),

            Step(8, "7. curse of blessing", [When.Timer(5), When.HpBetween(41, 80)],
                next: 6, delay: 10000, skill: CurseOfBlessing),

            // Phase two, which keeps the heartbeat alive so phase three can still arrive.
            Branch(7, "6. phase two > fear casting / fearful panic",
                [When.Timer(0), When.HpBelow(80), When.FirstTime(PhaseTwo)],
                Do.ArmTimer(0, 5000),
                Do.ArmTimer(5, 15000),
                FearPair[0],
                FearPair[1]),

            // --- 81-100: T1 -> T2 -> T3 -> T4 -> T1 -------------------------------------------------
            // Retail comments this one "magic orb" like its three siblings, but it casts flame spurt.
            // The action is reproduced, not the comment.
            Step(6, "5. magic orb [casts flame spurt]", [When.Timer(4), When.HpBetween(81, 100)],
                next: 1, delay: 10000, skill: FlameSpurt),

            Branch(5, "4. switch target / flame spurt", [When.Timer(3), When.HpBetween(81, 100)],
                Do.ArmTimer(4, 10000),
                Do.SwitchTarget(AggroTarget.RANDOM),
                Do.SkillOnTarget(FlameSpurt)),

            Step(4, "3. flame", [When.Timer(2), When.HpBetween(81, 100)],
                next: 3, delay: 10000, skill: Flame),

            Step(3, "2. magic orb", [When.Timer(1), When.HpBetween(81, 100)],
                next: 2, delay: 10000, skill: LargeMagicMissile),

            // The heartbeat, and the only thing keeping him going at exactly 80: the healthy chain
            // wants 81 or better and phase two wants strictly below 80, so that one value matches no
            // step at all until he drops another point.
            Branch(2, "HP recheck", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),
    };

    public DerakanakTheReaverAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
