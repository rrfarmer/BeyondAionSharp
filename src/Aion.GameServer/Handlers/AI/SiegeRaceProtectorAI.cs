using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/SiegeRaceProtectorAI (Source).</summary>
[AIName("siege_raceprotector")]
public class SiegeRaceProtectorAI : SiegeNpcAI
{
    public SiegeRaceProtectorAI(Npc owner)
        : base(owner)
    {
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.REWARD_AP_XP_DP_LOOT or AIQuestion.REWARD_LOOT => true,
            _ => base.Ask(question),
        };
    }
}
