using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/danuarReliquary/FinishThemAI (Estrayl).</summary>
[AIName("finish_them")]
public class FinishThemAI : NpcAI
{
    public FinishThemAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            AIActions.UseSkill(this, 21199);
            return ValueTask.CompletedTask;
        }, 1000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21199)
            GetOwner().GetController().Delete();
    }
}
