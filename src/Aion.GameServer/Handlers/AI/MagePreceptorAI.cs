using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Luzien
/// </summary>
[AIName("mage_preceptor")]
public class MagePreceptorAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 50, 25);

    public MagePreceptorAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        DespawnNpcs();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        DespawnNpcs();
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        DespawnNpcs();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 75:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19605, 10, GetRandomTarget()).UseNoAnimationSkill();
                break;
            case 50:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19606, 10, GetTarget()).UseNoAnimationSkill();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (!IsDead())
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19609, 10, GetOwner()).UseNoAnimationSkill();
                        ThreadPoolManager.GetInstance().Schedule(_ =>
                        {
                            WorldPosition p = GetPosition();
                            Spawn(282364, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                            Spawn(282363, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                            ScheduleSkill(2000);
                            return ValueTask.CompletedTask;
                        }, 4500L);
                    }
                    return ValueTask.CompletedTask;
                }, 3000L);
                break;
            case 25:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19606, 10, GetTarget()).UseNoAnimationSkill();
                ScheduleSkill(3000);
                ScheduleSkill(9000);
                ScheduleSkill(15000);
                break;
        }
    }

    private void ScheduleSkill(int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19605, 10, GetRandomTarget()).UseNoAnimationSkill();
            }
            return ValueTask.CompletedTask;
        }, (long)delay);
    }

    private Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 37);
    }

    private void DespawnNpcs()
    {
        DespawnNpc(GetPosition().GetWorldMapInstance().GetNpc(282364));
        DespawnNpc(GetPosition().GetWorldMapInstance().GetNpc(282363));
    }

    private void DespawnNpc(Npc npc)
    {
        if (npc != null)
        {
            npc.GetController().Delete();
        }
    }
}
