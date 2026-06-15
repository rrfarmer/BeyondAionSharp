using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/HarlequinLordReshkaSummonAI (@author Ritsu).</summary>
[AIName("harlequinlordreshkasummon")]
public class HarlequinLordReshkaSummonAI : AggressiveNpcAI
{
    public HarlequinLordReshkaSummonAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        Npc boss = GetPosition().GetWorldMapInstance().GetNpc(233453);
        if (boss != null && !boss.IsDead())
            GetAggroList().AddHate(boss.GetAggroList().GetTarget(AggroTarget.MOST_HATED), 1);
        base.HandleSpawned();
    }
}
