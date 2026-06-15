using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/SealBreakerAI (@author Estrayl).</summary>
[AIName("drakenspire_seal_breaker")]
public class SealBreakerAI : AggressiveNoLootNpcAI
{
    public SealBreakerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501334); // Arrrgh!
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        if (question == AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES)
            return true;
        return base.Ask(question);
    }
}
