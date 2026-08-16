using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The two frost summons of Danuar Reliquary — the tank, Danuar Reliquary Novun (284377), and the
/// dealer, Idean Lapilima (284378). Retail patterns <c>Rune_FrostNmd_TankSum_65_Ae</c> and
/// <c>Rune_FrostNmd_DealSum_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both ran on plain <c>aggressive</c>, and they are
/// the first bosses ported with their **casts translated** rather than left to npc_skills — their
/// patterns describe each branch, which is what made the skill indices resolvable.
/// <para>
/// Each runs a five-step chain that cycles: a strike, then its own speciality, alternating. The tank
/// opens by shielding itself and works a slower rotation; the dealer has no shield and hits more
/// often. Below half health either will round on a random attacker instead of the tank — once, and
/// then it stays angry.
/// </para>
/// <para>
/// **How the indices were resolved.** The tank's `on_wake_up` comment reads "cast defence buff
/// (skill 2)" and the branch casts index 2; Boost Physical Defense is the only BUFF in its list, so
/// index 2 is pinned. Index 0 then falls to Strike, which its branches label "single strike", and
/// index 1 to Insanity Eruption, labelled "area strike". The list is *rotated*, not offset — our data
/// lists the buff first because it carries <c>is_post_spawn</c>. One branch comment says "skill 1"
/// while casting index 2 and is simply stale; four others and the wake-up agree.
/// </para>
/// </remarks>
public abstract class DanuarFrostSummonAI : PatternAi
{
    /// <summary>Below half health it turns on someone else, and the flag keeps it turned.</summary>
    protected const int WentWild = 4;

    protected DanuarFrostSummonAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>The two branches every frost summon answers when hurt past half.</summary>
    protected static PatternBranch[] RoundsOnSomeoneElse() => Of(
        Branch(55, "random target", [When.Chance(50), When.HpBelow(50), When.FirstTime(WentWild)],
            Do.SwitchTarget(AggroTarget.RANDOM)),
        Branch(54, "random target", [When.HpBelow(50), When.FirstTime(WentWild)],
            Do.SwitchTarget(AggroTarget.MOST_HATED)));
}

/// <summary>Danuar Reliquary Novun (284377), the tank: shields itself, then a slower rotation.</summary>
[AIName("danuar_frost_tank")]
public sealed class DanuarFrostTankAI : DanuarFrostSummonAI
{
    private const int Strike = 16516;             // index 0 — "single strike"
    private const int InsanityEruption = 17949;   // index 1 — "area strike"
    private const int BoostPhysicalDefense = 17029; // index 2 — "defence buff", the only BUFF listed

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "cast defence buff", When.Always,
                Do.SkillOnSelf(BoostPhysicalDefense))),

        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, 8000),
                Do.SkillOnTarget(Strike))),

        OnBattleTimer = Of(
            Branch(6, "BT0", [When.Timer(0)], Do.ArmTimer(1, 8000), Do.SkillOnTarget(Strike)),
            Branch(5, "BT1", [When.Timer(1)], Do.ArmTimer(2, 13000), Do.SkillOnSelf(BoostPhysicalDefense)),
            Branch(4, "BT2", [When.Timer(2)], Do.ArmTimer(3, 10000), Do.SkillOnTarget(Strike)),
            Branch(3, "BT3", [When.Timer(3)], Do.ArmTimer(4, 13000), Do.SkillOnTarget(Strike)),
            Branch(2, "BT4", [When.Timer(4)], Do.ArmTimer(0, 13000), Do.SkillOnTarget(InsanityEruption))),

        OnAttacked = RoundsOnSomeoneElse(),
    };

    public DanuarFrostTankAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>Idean Lapilima (284378), the dealer: no shield, and a faster chain.</summary>
[AIName("danuar_frost_dealer")]
public sealed class DanuarFrostDealerAI : DanuarFrostSummonAI
{
    private const int Strike = 16540;      // index 0 — "single strike"
    private const int PowerAttack = 16984; // index 1 — "area strike"

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, 8000),
                Do.SkillOnTarget(Strike))),

        OnBattleTimer = Of(
            Branch(6, "BT0", [When.Timer(0)], Do.ArmTimer(1, 8000), Do.SkillOnTarget(Strike)),
            Branch(5, "BT1", [When.Timer(1)], Do.ArmTimer(2, 13000), Do.SkillOnTarget(PowerAttack)),
            Branch(4, "BT2", [When.Timer(2)], Do.ArmTimer(3, 8000), Do.SkillOnTarget(Strike)),
            Branch(3, "BT3", [When.Timer(3)], Do.ArmTimer(4, 10000), Do.SkillOnTarget(Strike)),
            Branch(2, "BT4", [When.Timer(4)], Do.ArmTimer(0, 13000), Do.SkillOnTarget(Strike))),

        OnAttacked = RoundsOnSomeoneElse(),
    };

    public DanuarFrostDealerAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
