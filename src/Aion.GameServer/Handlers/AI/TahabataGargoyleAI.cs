using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/TahabataGargoyleAI (Estrayl).</summary>
[AIName("tahabata_gargoyle")]
public class TahabataGargoyleAI : AggressiveNpcAI
{
    public TahabataGargoyleAI(Npc owner)
        : base(owner)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 18219)
            GetOwner().GetController().Delete();
    }
}
