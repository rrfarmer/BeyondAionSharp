using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.SkillEngine;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionShatterMineAI (Estrayl).</summary>
[AIName("eternal_bastion_shatter_mine")]
public class EternalBastionShatterMineAI : EternalBastionConstructAI
{
    public EternalBastionShatterMineAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetOwner().SetSeeState(CreatureSeeState.Search2);
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (creature is Player p && p.IsTransformed() && p.GetTransformModel().GetModelId() == 284320)
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21454, 1, p).UseNoAnimationSkill();
    }
}
