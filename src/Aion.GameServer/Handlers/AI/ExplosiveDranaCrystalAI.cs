using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/ExplosiveDranaCrystalAI (@author xTz).</summary>
[AIName("explosive_drana_crystal")]
public class ExplosiveDranaCrystalAI : ActionItemNpcAI
{
    private readonly AtomicBoolean isUsed = new(false);
    private ScheduledTask lifeTask;

    public ExplosiveDranaCrystalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartLifeTask();
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (isUsed.CompareAndSet(false, true))
        {
            WorldPosition p = GetPosition();
            Npc boss = p.GetWorldMapInstance().GetNpc(217308);
            if (boss != null && !boss.IsDead())
            {
                EffectController ef = boss.GetEffectController();
                if (ef.HasAbnormalEffect(19370))
                {
                    ef.RemoveEffect(19370);
                }
                else if (ef.HasAbnormalEffect(19371))
                {
                    ef.RemoveEffect(19371);
                }
                else if (ef.HasAbnormalEffect(19372))
                {
                    ef.RemoveEffect(19372);
                }
            }
            Npc npc = (Npc)Spawn(282530, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
            Npc invisibleNpc = (Npc)Spawn(282529, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
            SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 19373, 60, npc).UseNoAnimationSkill();
            SkillEngine.SkillEngine.GetInstance().GetSkill(invisibleNpc, 19654, 60, invisibleNpc).UseNoAnimationSkill();
            invisibleNpc.GetController().Delete();
            AIActions.DeleteOwner(this);
        }
    }

    private void CancelLifeTask()
    {
        if (lifeTask != null && !lifeTask.IsDone())
        {
            lifeTask.Cancel(true);
        }
    }

    private void StartLifeTask()
    {
        lifeTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
                AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, 60000L);
    }

    protected override void HandleDied()
    {
        CancelLifeTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelLifeTask();
        base.HandleDespawned();
    }
}
