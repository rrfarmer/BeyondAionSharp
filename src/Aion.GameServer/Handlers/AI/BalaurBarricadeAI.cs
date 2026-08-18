using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Dark Poeta's three balaur barricades (700517, 700556, 700558). Retail patterns
/// <c>ND2_H50_3</c>, <c>ND2_H50_4</c> and <c>ND2_KnQ</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Java parity was
/// <c>ai/instance/darkPoeta/BalaurBarricadeAI</c> (Ritsu, Estrayl), and the port of it was faithful —
/// what follows is a divergence from aionemu, not a fix to the port.
/// <para>
/// <b>Two of the three barricades had each other's reinforcement positions.</b> aionemu spawns 700517's
/// pair at (282, 1003) and 700556's at (315, 982); retail's <c>ND2_H50_3</c> — which the binding table
/// gives to <b>700517</b> — places them at (315, 982) and (308, 990), and <c>ND2_H50_4</c>, which is
/// 700556's, at (290, 1002) and (284, 1004). The two sets are transposed, so on our server two of the
/// three barricades called their guards to the far side of the room. 700558's positions were right.
/// </para>
/// <para>
/// <b>They were also the wrong NPCs.</b> Retail summons a dedicated trio — <c>...DrakanFighterSum</c>
/// (215452), <c>...DrakanKnSum</c> (215453) and <c>...DrakanWizardSum</c> (215451) — where aionemu used
/// the ones that stand around Dark Poeta already (215262, 215263, 214883). The names on screen match
/// pair for pair (proconsul, praefectus, magist), which is exactly why an observed port would pick them,
/// and the templates are different.
/// </para>
/// <para>
/// <b>And the trigger is not a health ladder.</b> aionemu reads <c>HpPhases(50, 10)</c> and spawns two
/// each time. Retail arms a <b>six-second</b> timer when the fight starts and polls: the first tick that
/// finds it below <b>seventy</b> percent calls two fighters and then <em>does not re-arm</em> — the
/// branch that spawns is the one branch that lets the clock stop. The other two come when it dies. So a
/// barricade burned down inside six seconds never calls its fighters at all, which no threshold port can
/// reproduce, and the four adds arrive in two roles rather than two waves.
/// </para>
/// <para>
/// <b>Not translated: the broadcast.</b> All three barricades broadcast <c>3409</c> to ten metres naming
/// whoever they are fighting, and retail's <c>XDrakan</c> answers it by switching target or by taking
/// hate and attacking — Dark Poeta's barricades stand in drakan camps, so a barricade pulls its
/// neighbours onto you. Nothing on our side listens for 3409, so sending it would be a broadcast into
/// silence; recorded in the fidelity log instead of shipped as a no-op.
/// </para>
/// <para>
/// <b>One event, where retail has two.</b> 700517 spawns its pair on <c>on_die</c>; 700556 and 700558 use
/// <c>on_killed_by_user</c>, so retail leaves nothing when something other than a player finishes them.
/// Our runtime raises a single death event and all three are treated as 700517 is. Nothing in Dark Poeta
/// kills a barricade except a player.
/// </para>
/// </remarks>
[AIName("balaurbarricade")]
public class BalaurBarricadeAI : OneDmgNoActionAI
{
    /// <summary><c>IDLF1_G_FeB_DrakanFighterSum_50_Ae</c> — anuhart proconsul.</summary>
    private const int Fighter = 215452;

    /// <summary><c>IDLF1_G_KeA_DrakanKnSum_50_Ae</c> — anuhart praefectus.</summary>
    private const int Knight = 215453;

    /// <summary><c>IDLF1_G_DrakanWizardSum_50_Ae</c> — anuhart magist.</summary>
    private const int Wizard = 215451;

    /// <summary>Retail's <c>live_time</c>, the same on all four adds of all three barricades.</summary>
    private const int GuardLifeSeconds = 300;

    /// <summary>The poll retail runs while the barricade is being attacked.</summary>
    private const int HeartbeatMillis = 6000;

    /// <summary>The one health guard in the pattern.</summary>
    private const int CallForHelpBelow = 70;

    /// <summary>Where one barricade puts its four guards.</summary>
    /// <param name="Fighters">Called once, below seventy percent.</param>
    /// <param name="Knight">Left behind when it dies.</param>
    /// <param name="Wizard">Left behind when it dies.</param>
    private readonly record struct Posting(SpawnSpot[] Fighters, SpawnSpot Knight, SpawnSpot Wizard);

    /// <summary>
    /// Retail writes headings as degrees; ours are the client's 0..120, which is degrees / 3
    /// (<see cref="PositionUtil.ConvertAngleToHeading"/>).
    /// </summary>
    private static SpawnSpot At(float x, float y, float z, int degrees)
        => new SpawnSpot(x, y, z, (sbyte)PositionUtil.ConvertAngleToHeading(degrees));

    private static readonly Dictionary<int, Posting> Postings = new Dictionary<int, Posting>
    {
        // ND2_H50_3 -- IDLF1_Barricade_Dragon.
        [700517] = new Posting(
            [At(315f, 982f, 111f, 141), At(308f, 990f, 113f, 324)],
            At(310f, 983f, 111f, 66),
            At(312f, 986f, 111f, 225)),

        // ND2_H50_4 -- IDLF1_Barricade_DragonB. The only one whose coordinates carry decimals.
        [700556] = new Posting(
            [At(290.71f, 1002.67f, 113.36f, 150), At(284.28f, 1004.98f, 113.3f, 354)],
            At(285f, 999f, 112f, 45),
            At(286f, 1002f, 112f, 252)),

        // ND2_KnQ -- IDLF1_Barricade_DragonC.
        [700558] = new Posting(
            [At(202f, 856f, 102f, 255), At(201f, 843f, 100f, 72)],
            At(205f, 845f, 100f, 153),
            At(200f, 847f, 100f, 339)),
    };

    private readonly object gate = new object();
    private ScheduledTask? heartbeat;
    private bool inCombat;
    private bool calledForHelp;

    public BalaurBarricadeAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>Where the barricade with this id posts its guards, for tests and for tables.</summary>
    internal static bool TryGetPosting(int npcId, out SpawnSpot[] fighters, out SpawnSpot knight, out SpawnSpot wizard)
    {
        if (Postings.TryGetValue(npcId, out Posting posting))
        {
            (fighters, knight, wizard) = (posting.Fighters, posting.Knight, posting.Wizard);
            return true;
        }

        (fighters, knight, wizard) = ([], default, default);
        return false;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);

        // on_enter_attack_state: arm the poll once, on the swing that starts the fight.
        lock (gate)
        {
            if (inCombat)
                return;
            inCombat = true;
        }

        ArmHeartbeat();
    }

    private void ArmHeartbeat()
    {
        lock (gate)
        {
            CancelHeartbeat();
            heartbeat = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Tick();
                return ValueTask.CompletedTask;
            }, HeartbeatMillis);
        }
    }

    /// <summary>
    /// One poll. Below seventy for the first time it calls the fighters and lets the clock stop —
    /// retail's higher-priority branch is the one that does not re-arm — and otherwise polls again.
    /// </summary>
    private void Tick()
    {
        lock (gate)
        {
            heartbeat = null;
            if (IsDead() || !inCombat)
                return;

            if (!calledForHelp && GetLifeStats().GetHpPercentage() < CallForHelpBelow)
            {
                calledForHelp = true;
                if (Postings.TryGetValue(GetNpcId(), out Posting posting))
                    foreach (SpawnSpot spot in posting.Fighters)
                        SpawnGuard(Fighter, spot);
                return;
            }
        }

        ArmHeartbeat();
    }

    protected override void HandleDied()
    {
        lock (gate)
        {
            CancelHeartbeat();
            inCombat = false;
        }

        if (Postings.TryGetValue(GetNpcId(), out Posting posting))
        {
            SpawnGuard(Knight, posting.Knight);
            SpawnGuard(Wizard, posting.Wizard);
        }

        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        Reset();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        Reset();
        base.HandleDespawned();
    }

    private void Reset()
    {
        lock (gate)
        {
            CancelHeartbeat();
            inCombat = false;
            calledForHelp = false;
        }
    }

    private void CancelHeartbeat()
    {
        if (heartbeat != null && !heartbeat.IsDone())
            heartbeat.Cancel(true);
        heartbeat = null;
    }

    /// <summary>Places one guard and gives it retail's five minutes.</summary>
    /// <remarks>
    /// Written by hand here before <c>SpawnFor</c> existed, and behaviourally identical to it. Moved onto
    /// the shared helper so this class stops reading as a missing lifetime in
    /// <c>audit_spawn_lifetimes.py</c> -- it was that audit's own documented false positive, and a
    /// caveat that can be retired by a one-line change is better retired than repeated.
    /// </remarks>
    private void SpawnGuard(int npcId, SpawnSpot spot)
        => SpawnFor(npcId, spot.X, spot.Y, spot.Z, spot.Heading, GuardLifeSeconds);
}
