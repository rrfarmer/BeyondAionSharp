using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/raksang/IllusionMasterSharikAI (@author xTz).
/// </summary>
[AIName("illusion_maseter_sharik")]
public class IllusionMasterSharikAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(80);
    private readonly AtomicBoolean startedEvent = new AtomicBoolean(false);
    private int position = 1;
    private int percent = 100;
    private ScheduledTask? phaseTask;

    public IllusionMasterSharikAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        WorldPosition p = GetPosition();
        if (p.GetX() == 738.065f && p.GetY() == 311.606f)
        {
            position = 1;
        }
        else
        {
            position = 2;
        }
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player player && PositionUtil.IsInRange(GetOwner(), player, 30))
        {
            if (startedEvent.CompareAndSet(false, true))
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1401112);
            }
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), 1401136);
        if (position == 1)
        {
            Spawn(730446, 738.766f, 317.482f, 911.897f, (sbyte)0, 5);
        }
        else
        {
            Spawn(730447, 735.909f, 265.696f, 911.897f, (sbyte)0, 278);
        }
        StartPhaseTask();
    }

    private void StartPhaseTask()
    {
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1401114);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19981, 46, GetOwner()).UseNoAnimationSkill();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (!IsDead())
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19901, 44, GetOwner()).UseNoAnimationSkill();
                        if (percent <= 50)
                        {
                            ThreadPoolManager.GetInstance().Schedule(_ =>
                            {
                                if (!IsDead())
                                {
                                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19903, 44, GetOwner()).UseNoAnimationSkill();
                                }
                                return ValueTask.CompletedTask;
                            }, 13000L);
                        }
                    }
                    return ValueTask.CompletedTask;
                }, 3000L);
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(3000), System.TimeSpan.FromMilliseconds(40000));
    }

    private void CancelPhaseTask()
    {
        if (phaseTask != null && !phaseTask.IsDone())
        {
            phaseTask.Cancel(true);
        }
    }

    protected override void HandleBackHome()
    {
        DespawnMirrors();
        CancelPhaseTask();
        base.HandleBackHome();
        PacketSendUtility.BroadcastToMap(GetOwner(), 1401137);
        if (position == 1)
        {
            Spawn(217425, 736.21704f, 270.8546f, 910.678f, (sbyte)53);
        }
        else
        {
            Spawn(217425, 738.065f, 311.606f, 910.678f, (sbyte)53);
        }
        AIActions.DeleteOwner(this);
    }

    private void DespawnMirrors()
    {
        WorldPosition p = GetPosition();
        if (p != null)
        {
            WorldMapInstance instance = p.GetWorldMapInstance();
            if (instance != null)
            {
                DeleteNpcs(instance.GetNpcs(730446));
                DeleteNpcs(instance.GetNpcs(730447));
            }
        }
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    protected override void HandleDespawned()
    {
        CancelPhaseTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        DespawnMirrors();
        CancelPhaseTask();
        GetPosition().GetWorldMapInstance().SetDoorState(294, true);
        GetPosition().GetWorldMapInstance().SetDoorState(295, true);
        base.HandleDied();
    }
}
