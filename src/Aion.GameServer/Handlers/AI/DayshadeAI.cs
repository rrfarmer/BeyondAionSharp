using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyssal_splinter/DayshadeAI (Luzien, Ritsu).</summary>
[AIName("dayshade")]
public class DayshadeAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);

    public DayshadeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            AIActions.Die(this, creature);
            Spawn(216949, 455.5502f, 702.09485f, 433.13727f, (sbyte)108); // ebonsoul
            Spawn(216948, 447.1937f, 683.72217f, 433.1805f, (sbyte)108); // rukril
            AIActions.DeleteOwner(this);
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        isHome.Set(true);
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
