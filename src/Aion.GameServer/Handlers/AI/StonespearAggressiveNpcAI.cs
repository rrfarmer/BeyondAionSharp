using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/stonespearReach/StonespearAggressiveNpcAI (Yeats).</summary>
[AIName("aggressive_stonespear")]
public class StonespearAggressiveNpcAI : AggressiveNoLootNpcAI
{
    private readonly List<int> guardIds = new List<int>();

    public StonespearAggressiveNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        FindGuardianStone();
    }

    private void FindGuardianStone()
    {
        guardIds.AddRange(new[] { 855763, 855832, 855786, 856466, 856467, 856468 });
        Creature target = null;
        foreach (int npcId in guardIds)
        {
            target = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(npcId);
            if (target != null)
            {
                break;
            }
        }
        if (target != null)
        {
            GetOwner().GetAggroList().AddHate(target, 3000);
            SetStateIfNot(AIState.FIGHT);
            Think();
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
