using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionSiegeRamAI (Estrayl).</summary>
[AIName("eternal_bastion_siege_ram")]
public class EternalBastionSiegeRamAI : EternalBastionAssaulterNpcAI
{
    public EternalBastionSiegeRamAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (creature is Npc)
        {
            GetOwner().GetAggroList().AddHate(creature, 500000);
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20778, 1, GetTarget()).UseSkill();
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20778, 1, GetTarget()).UseSkill();
    }
}
