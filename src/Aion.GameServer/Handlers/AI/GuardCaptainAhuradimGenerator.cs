using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/sauroBase/GuardCaptainAhuradimGenerator.</summary>
[AIName("guard_captain_ahuradim_generator")]
public class GuardCaptainAhuradimGenerator : NoActionAI
{
    public GuardCaptainAhuradimGenerator(Npc owner)
        : base(owner)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21200)
        {
            VisibleObject? guardCaptainAhuradim = GetKnownList().FindObject(o => o.Get() is Npc npc && npc.GetNpcId() == 230857);
            if (guardCaptainAhuradim != null)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21191, 1, guardCaptainAhuradim).UseSkill();
        }
    }
}
