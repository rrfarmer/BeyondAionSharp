using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The hazards Tiamat's incarnations leave on the ground. Retail patterns
/// <c>LDF4b_Tiamat_Temp01</c> through <c>_Temp11</c> and their hard-mode twins.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/dragonLordsRefuge/TiamatsIncarnationSpawnsAI (@author Estrayl).
/// Retail-sourced correction below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Every one of these fired once in retail and forever here.</b> A hazard waits — two seconds for
/// the earth pair, six for the rest — then spawns an <c>_invisible</c> twin at its own point for two
/// seconds, and that twin casts once and despawns. <b>One pulse per hazard, ever.</b> There is no
/// <c>set_idle_timer</c> on the rung that does it, so nothing re-arms.
/// </para>
/// <para>
/// This class cast every three seconds from two and a half, for as long as the hazard stood — so a
/// hazard that retail lets tick once was ticking five or ten times, and the ground under an incarnation
/// fight did an order of magnitude more damage than it should.
/// </para>
/// <para>
/// The invisible twin is collapsed into the hazard here, which is the same arrangement this port uses
/// for Calindi's crown and Chantra's rings: the visible npc casts rather than spawning a caster. What
/// was wrong was the count and the delay, not the collapse.
/// </para>
/// <para>
/// <b>Not translated.</b> The earthquake's fifteen-second swap: retail arms a battle timer when it is
/// engaged and turns <c>Crack_EarthQuake</c> into <c>Crack_BrokenGround</c> for four minutes. This port
/// treats the two as separate hazards the boss places, so the transition does not exist.
/// </para>
/// </remarks>
[AIName("tiamats_incarnation_spawn")]
public class TiamatsIncarnationSpawnsAI : NpcAI
{
    private ScheduledTask skillTask;

    public TiamatsIncarnationSpawnsAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail's <c>set_idle_timer</c> on each hazard: two seconds for the earth pair, six for the rest.
    /// </summary>
    /// <remarks>
    /// Keyed by npc id because the two generations do not agree — 282735 and 282737 are the earth pair
    /// in normal mode, and in hard mode it is 856068 and 856070, which are numerically where the
    /// gravity balls sit in the other set.
    /// </remarks>
    private static readonly System.Collections.Generic.HashSet<int> QuickHazards =
        [282735, 282737, 856068, 856070];

    private const long QuickMillis = 2000L;
    private const long SlowMillis = 6000L;

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        // Once. Retail's rung carries no set_idle_timer, so the hazard pulses a single time however
        // long it stands.
        skillTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.UseSkill(this, GetSkillId());
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, QuickHazards.Contains(GetNpcId()) ? QuickMillis : SlowMillis);
    }

    protected override void HandleDespawned()
    {
        if (skillTask != null && !skillTask.IsDone())
            skillTask.Cancel(true);
        base.HandleDespawned();
    }

    private int GetSkillId()
    {
        switch (GetNpcId())
        {
            case 282727: // Gravity Whirlpool
            case 856074:
                return 20155;
            case 282729: // Thunderbolt Whirlpool
            case 856076:
                return 20156;
            case 282731: // Petrification Crystal
            case 856072:
                return 20159;
            case 282735: // Cavity of Earth
            case 856068:
                return 20172;
            case 282737: // Collapsing Earth
            case 856070:
                return 20173;
            default:
                return 0;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}
