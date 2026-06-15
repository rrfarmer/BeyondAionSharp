using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/sauroBase/SauroDroneAI (Estrayl).</summary>
[AIName("sauro_drone")]
public class SauroDroneAI : AggressiveNpcAI
{
    public SauroDroneAI(Npc owner)
        : base(owner)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 19498)
            GetOwner().GetController().Delete();
    }
}
