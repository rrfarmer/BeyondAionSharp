using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Chief maid Miladi, Adma Stronghold (214693). Retail pattern <c>ND2_WeG</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>She had no AI class at all</b> — she ran
/// <c>aggressive</c>, so a fight with sixteen timer branches and six summons was a plain melee npc.
/// <para>
/// <b>Her mechanic is that the adds do not come to her.</b> Every succubus is placed
/// <i>on a player</i> — the current target when she engages, then the second and third most hated as the
/// fight goes on — so the raid's healers and casters get one each rather than the tank. That is what
/// <c>spawn_on_target_by_attacker_indicator</c> means, and it is why this fight cannot be approximated by
/// spawning adds at her feet.
/// </para>
/// <list type="bullet">
/// <item><b>on engaging</b> — one succubus on whoever she is fighting, and two clocks start</item>
/// <item><b>75-31</b> — a succubus on the second most hated, once, then a fifteen-second clock</item>
/// <item><b>below 30</b> — a succubus on the <i>second and third</i> most hated, once, and she turns on
/// the third; a ten-second clock then keeps placing one on the third most hated</item>
/// </list>
/// <para>
/// <b>Not translated.</b> Two skill indices (<c>SKILLI_INDEX_0</c> and <c>_1</c>, both cast on the second
/// or third most hated) and the <c>say_to_all</c> each branch carries, whose string id is unresolved.
/// The timers, the summons and the target switch are all of retail's structure that this port can state.
/// </para>
/// </remarks>
[AIName("chief_maid_miladi")]
public class ChiefMaidMiladiAI : PatternAi
{
    /// <summary>Retail <c>BIDDF2A_SuccubusSum_50_An</c>, twelve seconds, on the player exactly.</summary>
    /// <remarks>
    /// <b>Fifty is retail's <c>valid_distance</c>, not its <c>spawn_range</c>.</b> The first version of
    /// this class read it as scatter, which put the succubi anywhere within fifty metres of the player
    /// instead of on them — and since that is further than Miladi stands from the raid, an add could
    /// land nearer her than her victim, which is precisely the fight this class exists to avoid
    /// approximating. <c>spawn_range</c> is zero: they arrive underfoot.
    /// </remarks>
    private const int Succubus = 280963;
    private const int SuccubusLife = 12;
    private const float Eligible = 50f;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: every summon shares one group.</summary>
    private const int Summons = 1;

    /// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> and <c>_2</c> — each band opens once.</summary>
    private const int LowBandOpened = 1;
    private const int MidBandOpened = 2;

    private const int Heartbeat = 0;
    private const int LowClock = 1;
    private const int MidClock = 2;
    private const int OpenerClock = 3;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(OpenerClock, 15000),
                Do.SpawnOnAttacker(AggroTarget.MOST_HATED, Succubus, Summons,
                    validDistance: Eligible, liveSeconds: SuccubusLife))),

        OnBattleTimer = Of(
            // Below thirty she opens on two players at once and turns on the third most hated.
            Branch(6, "below 30, opening", [When.Timer(Heartbeat), When.HpBelow(30),
                    When.FirstTime(LowBandOpened)],
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(LowClock, 10000),
                Do.SpawnOnAttacker(AggroTarget.SECOND_MOST_HATED, Succubus, Summons,
                    validDistance: Eligible, liveSeconds: SuccubusLife),
                Do.SpawnOnAttacker(AggroTarget.THIRD_MOST_HATED, Succubus, Summons,
                    validDistance: Eligible, liveSeconds: SuccubusLife),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(5, "75-31, opening", [When.Timer(Heartbeat), When.HpBetween(31, 75),
                    When.FirstTime(MidBandOpened)],
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(MidClock, 15000),
                Do.SpawnOnAttacker(AggroTarget.SECOND_MOST_HATED, Succubus, Summons,
                    validDistance: Eligible, liveSeconds: SuccubusLife)),

            // The two band clocks, which keep placing succubi once their band has opened.
            Branch(4, "below 30, repeating", [When.Timer(LowClock), When.HpBelow(30)],
                Do.ArmTimer(LowClock, 10000),
                Do.SpawnOnAttacker(AggroTarget.THIRD_MOST_HATED, Succubus, Summons,
                    validDistance: Eligible, liveSeconds: SuccubusLife)),

            // Retail's heartbeat, so the chain keeps ticking between bands. Without it a fight that
            // opens the 75-31 band and then drops below thirty never reaches the branch above.
            Branch(1, "", [When.Timer(Heartbeat)],
                Do.ArmTimer(Heartbeat, 5000))),
    };

    public ChiefMaidMiladiAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
