using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionSkyAssaulterAI (@author Estrayl).</summary>
[AIName("ahserion_sky_assaulter")]
public class AhserionSkyAssaulterAI : GeneralNpcAI
{
    public AhserionSkyAssaulterAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { Activate(); return ValueTask.CompletedTask; }, 400L);
    }

    /// <summary>
    /// Retail <c>BGab1_Sub</c>: the assault pod stands five seconds and the troopers it lands two hours.
    /// </summary>
    /// <remarks>
    /// <b>The pod was already bounded, at six seconds rather than retail's five</b> — close enough to
    /// look right and not be. The troopers had no bound at all; two hours is not a mechanic, but a siege
    /// that runs long should not end with every wave still standing.
    /// </remarks>
    private const long PodLifeMillis = 5000L;

    /// <summary>
    /// Retail's <c>z</c> on all three of the strike pod's troopers: three metres up, not at its feet.
    /// </summary>
    /// <remarks>
    /// The TBM pod's three (<c>x=-2 y=2</c>, <c>x=2 y=-2</c>, <c>x=-2 y=-2 z=2</c>) were already right
    /// down to the one that does carry a z, which is what made the strike pod's zeroes worth checking
    /// rather than assuming.
    /// </remarks>
    public const float StrikeWaveUp = 3f;
    /// <summary>
    /// <b>Deliberately not applied.</b> Retail gives these troopers 7,200 seconds and
    /// <c>AhserionAggressiveNpcAI</c> already removes them after eight minutes.
    /// </summary>
    /// <remarks>
    /// Two hours on a siege map is retail's backstop against leaks, not a mechanic; eight minutes is a
    /// real bound on how long a landed wave lives. <b>Loosening it fifteenfold in the name of fidelity
    /// would make the encounter measurably worse</b>, and the number this log first applied here was dead
    /// code anyway, since the shorter clock always won. The divergence is recorded rather than removed.
    /// </remarks>
    private const int TrooperLifeNotApplied = 7200;

    private void Activate()
    {
        WorldPosition p = GetPosition();
        Npc assaultPod = (Npc)Spawn(297188, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading()); // Assault Pod
        assaultPod.GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { assaultPod.GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, PodLifeMillis));

        ThreadPoolManager.GetInstance().Schedule(_ => { UseSkill(); return ValueTask.CompletedTask; }, 1000L);

        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnDefenders(); return ValueTask.CompletedTask; }, 4000L);
    }

    private void UseSkill()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20776, 1, GetOwner()).UseWithoutPropSkill(); // Trooper Shock
    }

    private void SpawnDefenders()
    {
        WorldPosition p = GetPosition();
        switch (GetNpcId())
        {
            case 297352:
                // Retail's three are RELATIVE x=3 y=-3 z=3, z=3, x=3 y=3 z=3 — the same z on all
                // three. This port had 0, 0.1 and 0, so the strike pod's wave arrived at the pod's own
                // feet instead of three metres above it. The TBM pod below already matched.
                Spawn(297191, p.GetX() + 3, p.GetY() - 3, p.GetZ() + StrikeWaveUp, (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
                Spawn(297192, p.GetX(), p.GetY(), p.GetZ() + StrikeWaveUp, (sbyte)p.GetHeading()); // Ahserion Troopers Sorcerer
                Spawn(297191, p.GetX() + 3, p.GetY() + 3, p.GetZ() + StrikeWaveUp, (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
                break;
            case 297353:
                Spawn(297190, p.GetX() - 2, p.GetY() + 2, p.GetZ(), (sbyte)p.GetHeading()); // Ahserion Troopers Defender Captain
                Spawn(297191, p.GetX() + 2, p.GetY() - 2, p.GetZ(), (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
                Spawn(297191, p.GetX() - 2, p.GetY() - 2, p.GetZ() + 2, (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
                break;
        }

        AIActions.DeleteOwner(this);
    }
}
