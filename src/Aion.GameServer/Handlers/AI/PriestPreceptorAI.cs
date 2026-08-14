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
[AIName("priest_preceptor")]
public class PriestPreceptorAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail thresholds, from pattern IDArena_S7_Named_4: a skill at 80% and the add wave at
    /// 30%, where we had an invented 75/25. See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(80, 30);

    public PriestPreceptorAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19612, 15, GetOwner()).UseNoAnimationSkill(); return ValueTask.CompletedTask; }, 1000L);
    }

    protected override void HandleBackHome()
    {
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
            case 80:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19611, 10, GetRandomTarget()).UseNoAnimationSkill();
                break;
            case 30:
                StartEvent();
                break;
        }
    }

    private void StartEvent()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19610, 10, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19614, 10, GetOwner()).UseNoAnimationSkill();

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                WorldPosition p = GetPosition();
                ApplySoulSickness((Npc)Spawn(282366, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading()));
                ApplySoulSickness((Npc)Spawn(282367, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading()));
                ApplySoulSickness((Npc)Spawn(282368, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading()));
                return ValueTask.CompletedTask;
            }, 5000L);
            return ValueTask.CompletedTask;
        }, 2000L);
    }

    private Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 25);
    }

    private void ApplySoulSickness(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            npc.GetLifeStats().SetCurrentHpPercent(50); // TODO: remove this, fix max hp debuffs not reducing current hp properly
            SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 19594, 4, npc).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, 1000L);
    }
}
