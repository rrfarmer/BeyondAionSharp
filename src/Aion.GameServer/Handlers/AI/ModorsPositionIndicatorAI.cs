using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/danuarReliquary/ModorsPositionIndicatorAI (Yeats).</summary>
[AIName("modors_position_indicator")]
public class ModorsPositionIndicatorAI : NpcAI
{
    public ModorsPositionIndicatorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21166, 1, GetOwner()).UseWithoutPropSkill();
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21166)
            GetOwner().GetController().Delete();
    }
}
