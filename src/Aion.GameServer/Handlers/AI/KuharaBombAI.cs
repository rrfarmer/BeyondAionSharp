using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/KuharaBombAI (@author xTz).</summary>
[AIName("kuhara_bomb")]
public class KuharaBombAI : GeneralNpcAI
{
    private readonly AtomicBoolean isDestroyed = new AtomicBoolean(false);
    private Npc boss;

    public KuharaBombAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        this.SetStateIfNot(AIState.FOLLOWING);
        boss = GetPosition().GetWorldMapInstance().GetNpc(217311);
    }

    protected override void HandleMoveArrived()
    {
        if (isDestroyed.CompareAndSet(false, true))
        {
            if (boss != null && !boss.IsDead())
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19659, 60, boss).UseNoAnimationSkill();
            }
        }
    }
}
