using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/GeniesIncenseBurnerAI (@author xTz).</summary>
[AIName("geniesincenseburner")]
public class GeniesIncenseBurnerAI : ActionItemNpcAI
{
    public GeniesIncenseBurnerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        AIActions.TargetSelf(this);
        AIActions.UseSkill(this, 18465);
    }
}
