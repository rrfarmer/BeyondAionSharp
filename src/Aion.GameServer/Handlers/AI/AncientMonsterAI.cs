using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/levinshor/AncientMonsterAI (Yeats).</summary>
[AIName("LDF4_Advance_Ancient_Monster")]
public class AncientMonsterAI : AggressiveNpcAI
{
    public AncientMonsterAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetOwner().GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
                GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, 1000L * 60 * 60));
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetDistanceToSpawnLocation() > 15)
            TargetEventHandler.OnTargetGiveup(this);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        float multi = 1.5f;
        if (effect != null && effect.GetSkillTemplate().GetSkillId() == 21780)
            multi = 3;
        return base.ModifyOwnerDamage(damage * multi, effected, effect);
    }
}
