using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/raksang/SealGeneratorAI (@author xTz).</summary>
[AIName("seal_generator")]
public class SealGeneratorAI : AggressiveNpcAI
{
    private readonly AtomicBoolean startedEvent = new AtomicBoolean();

    public SealGeneratorAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player player)
        {
            if (PositionUtil.GetDistance(GetOwner(), player) <= 30 && startedEvent.CompareAndSet(false, true))
            {
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401156);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 1;
    }
}
