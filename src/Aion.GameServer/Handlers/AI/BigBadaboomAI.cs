using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/BigBadaboomAI.</summary>
[AIName("big_badaboom")]
public class BigBadaboomAI : ActionItemNpcAI
{
    public BigBadaboomAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        int morphSkill = 0;
        switch (GetNpcId())
        {
            case 231016: // Big Badaboom.
            case 231017: // Bigger Badaboom.
                morphSkill = 0x4E502E;
                break;
        }

        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), morphSkill >> 8, morphSkill & 0xFF, player).UseNoAnimationSkill();
        AIActions.DeleteOwner(this);
    }
}
