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
    private const int TrooperLife = 7200;

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
                SpawnFor(297191, p.GetX() + 3, p.GetY() - 3, p.GetZ(), (sbyte)p.GetHeading(), TrooperLife); // Ahserion Troopers Assassin
                SpawnFor(297192, p.GetX(), p.GetY(), p.GetZ() + 0.1f, (sbyte)p.GetHeading(), TrooperLife); // Ahserion Troopers Sorcerer
                SpawnFor(297191, p.GetX() + 3, p.GetY() + 3, p.GetZ(), (sbyte)p.GetHeading(), TrooperLife); // Ahserion Troopers Assassin
                break;
            case 297353:
                SpawnFor(297190, p.GetX() - 2, p.GetY() + 2, p.GetZ(), (sbyte)p.GetHeading(), TrooperLife); // Ahserion Troopers Defender Captain
                Spawn(297191, p.GetX() + 2, p.GetY() - 2, p.GetZ(), (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
                Spawn(297191, p.GetX() - 2, p.GetY() - 2, p.GetZ() + 2, (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
                break;
        }

        AIActions.DeleteOwner(this);
    }
}
