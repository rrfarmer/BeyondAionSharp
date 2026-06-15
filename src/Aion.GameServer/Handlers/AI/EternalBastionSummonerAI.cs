using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionSummonerAI (@author Estrayl).</summary>
[AIName("eternal_bastion_summoner")]
public class EternalBastionSummonerAI : SummonerAI
{
    public EternalBastionSummonerAI(Npc owner)
        : base(owner)
    {
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
