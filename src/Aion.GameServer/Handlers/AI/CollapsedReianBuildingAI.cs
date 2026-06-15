using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/CollapsedReianBuildingAI (xTz).</summary>
[AIName("collapsed_reian_building")]
public class CollapsedReianBuildingAI : NpcAI
{
    public CollapsedReianBuildingAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20088, 60, GetOwner()).UseNoAnimationSkill();
    }
}
