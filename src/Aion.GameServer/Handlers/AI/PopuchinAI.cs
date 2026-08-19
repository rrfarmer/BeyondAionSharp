using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/PopuchinAI (xTz).</summary>
[AIName("popuchin")]
public class PopuchinAI : AggressiveNpcAI
{
    /// <summary>The two bombs he puts out: guided above half health, scattered below it.</summary>
    public const int GuidedBomb = 217374;
    public const int ScatteredBomb = 217375;

    private bool isHome = true;
    private ScheduledTask bombTask;

    public PopuchinAI(Npc owner)
        : base(owner)
    {
    }

    private void StartBombTask()
    {
        if (!IsDead() && !isHome)
        {
            bombTask = ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                if (!IsDead() && !isHome)
                {
                    VisibleObject target = GetTarget();
                    if (target is Player)
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19413, 49, target).UseNoAnimationSkill();
                    }
                    ThreadPoolManager.GetInstance().Schedule(ct2 =>
                    {
                        if (!IsDead() && !isHome)
                        {
                            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19412, 49, GetOwner()).UseNoAnimationSkill();
                            ThreadPoolManager.GetInstance().Schedule(ct3 =>
                            {
                                if (!IsDead() && !isHome && GetOwner().IsSpawned())
                                {
                                    if (GetLifeStats().GetHpPercentage() > 50)
                                    {
                                        WorldPosition p = GetPosition();
                                        if (p != null && p.GetWorldMapInstance() != null)
                                        {
                                            Spawn(GuidedBomb, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                                            Spawn(GuidedBomb, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                                            StartBombTask();
                                        }
                                    }
                                    else
                                    {
                                        SpawnRndBombs();
                                        StartBombTask();
                                    }
                                }
                                return ValueTask.CompletedTask;
                            }, 1500L);
                        }
                        return ValueTask.CompletedTask;
                    }, 3000L);
                }
                return ValueTask.CompletedTask;
            }, 15500L);
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome)
        {
            isHome = false;
            GetPosition().GetWorldMapInstance().SetDoorState(68, false);
            StartBombTask();
        }
    }

    /// <summary>
    /// Retail's <c>on_leave_attack_state</c>: <c>control_door</c> and <c>despawn spawn_id=SPAWN_ID_1</c>.
    /// </summary>
    /// <remarks>
    /// <b>The despawn was missing.</b> Every bomb he had put out stayed where it was when he reset, and
    /// the guided ones carried a ten-second self-delete only because that class had invented one. With
    /// the bomb's clock moved onto retail's aggro timer — where it belongs — this is the only thing that
    /// clears a bomb nobody ever went near, which is exactly the job retail gives it.
    /// </remarks>
    protected override void HandleBackHome()
    {
        isHome = true;
        base.HandleBackHome();
        GetPosition().GetWorldMapInstance().SetDoorState(68, true);
        if (bombTask != null && !bombTask.IsDone())
        {
            bombTask.Cancel(true);
        }

        DespawnBombs();
    }

    /// <summary>Retail's <c>SPAWN_ID_1</c> for this boss: both bomb npcs.</summary>
    private void DespawnBombs()
    {
        WorldMapInstance instance = GetPosition()?.GetWorldMapInstance();
        if (instance == null)
            return;

        foreach (Npc bomb in instance.GetNpcs(GuidedBomb, ScatteredBomb))
        {
            if (bomb != null && !bomb.GetLifeStats().IsAboutToDie() && bomb.IsSpawned())
                bomb.GetController().Delete();
        }
    }

    private void SpawnRndBombs()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead() && !isHome)
            {
                for (int i = 0; i < 10; i++)
                {
                    RndSpawnInRange(ScatteredBomb, 1, 12);
                }
            }
            return ValueTask.CompletedTask;
        }, 1500L);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetPosition().GetWorldMapInstance().SetDoorState(68, true);
    }
}
