using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/UnstablePazuzuWormAI (@author Luzien).</summary>
[AIName("unstablepazuzuworm")]
public class UnstablePazuzuWormAI : AggressiveNpcAI
{
    public UnstablePazuzuWormAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.TargetCreature(this, GetPosition().GetWorldMapInstance().GetNpc(219554));
            AIActions.UseSkill(this, 19291);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 3000L);
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.REWARD_AP:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
