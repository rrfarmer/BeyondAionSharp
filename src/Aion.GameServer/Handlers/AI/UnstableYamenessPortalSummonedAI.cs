using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/UnstableYamenessPortalSummonedAI (Ritsu, Cheatkiller).</summary>
[AIName("unstableyamenessportal")]
public class UnstableYamenessPortalSummonedAI : AggressiveNpcAI
{
    public UnstableYamenessPortalSummonedAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnSummons(); return ValueTask.CompletedTask; }, 12000L);
    }

    private void SpawnSummons()
    {
        if (IsDead() || !GetOwner().IsSpawned()) // ensure npc is still alive and instance is not destroyed yet
            return;
        Spawn(219565, GetOwner().GetX() + 3, GetOwner().GetY() - 3, GetOwner().GetZ(), (sbyte)0);
        Spawn(219566, GetOwner().GetX() - 3, GetOwner().GetY() + 3, GetOwner().GetZ(), (sbyte)0);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetOwner().IsSpawned())
            {
                Spawn(219565, GetOwner().GetX() + 3, GetOwner().GetY() - 3, GetOwner().GetZ(), (sbyte)0);
                Spawn(219566, GetOwner().GetX() - 3, GetOwner().GetY() + 3, GetOwner().GetZ(), (sbyte)0);
            }
            return ValueTask.CompletedTask;
        }, 60000L);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
