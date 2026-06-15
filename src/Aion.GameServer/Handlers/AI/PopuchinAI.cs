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
                                            Spawn(217374, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                                            Spawn(217374, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
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

    protected override void HandleBackHome()
    {
        isHome = true;
        base.HandleBackHome();
        GetPosition().GetWorldMapInstance().SetDoorState(68, true);
        if (bombTask != null && !bombTask.IsDone())
        {
            bombTask.Cancel(true);
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
                    RndSpawnInRange(217375, 1, 12);
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
