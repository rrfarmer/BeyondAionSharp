using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/DranaLumpAI (@author Ritsu, Estrayl).</summary>
[AIName("drana_lump")]
public class DranaLumpAI : ActionItemNpcAI
{
    public DranaLumpAI(Npc owner)
        : base(owner)
    {
    }

    public override void HandleCreatureDetected(Creature creature)
    {
        if (creature is Npc)
        {
            switch (((Npc)creature).GetNpcId())
            {
                case 214880:
                case 215388:
                case 215389:
                    CheckDistance((Npc)creature);
                    break;
            }
        }
    }

    private void CheckDistance(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (PositionUtil.GetDistance(GetOwner(), npc) <= 2)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18536, 46, npc).UseSkill();
            else
                CheckDistance(npc);
            return ValueTask.CompletedTask;
        }, 4000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 18536)
            GetOwner().GetController().Delete();
    }
}
