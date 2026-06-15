using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionSiegeMineAI (ViAl, Estrayl).</summary>
[AIName("eternal_bastion_siege_mine")]
public class EternalBastionSiegeMineAI : EternalBastionConstructAI
{
    private readonly AtomicBoolean isActivated = new AtomicBoolean();

    public EternalBastionSiegeMineAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isActivated.CompareAndSet(false, true))
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21143, 1, GetOwner()).UseWithoutPropSkill();
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21143)
        {
            ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20549, 1, GetOwner()).UseSkill(); return ValueTask.CompletedTask; }, 750L);
            Spawn(GetNpcId() == 831330 ? 284696 : 284695, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ() + 1, (sbyte)0);
        }
    }
}
