using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tallocsHollow/KinquidDebuffAI (@author xTz).</summary>
[AIName("kinquid_debuff")]
public class KinquidDebuffAI : AggressiveNpcAI
{
    public KinquidDebuffAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        base.HandleCreatureMoved(creature);
        if (creature is Npc npc && npc.GetNpcId() == 215467 && IsInRange(creature, 2)) // kinquid within debuff range
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), GetNpcId() == 282008 ? 19235 : 19236, 46, GetOwner()).UseNoAnimationSkill();
        }
    }
}
