using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/IDTiamat_2_NPC_TiamatAI (Estrayl).</summary>
[AIName("IDTiamat_2_NPC_Tiamat")]
public class IDTiamat_2_NPC_TiamatAI : GeneralNpcAI
{
    public IDTiamat_2_NPC_TiamatAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBeforeSpawned()
    {
        base.HandleBeforeSpawned();
        GetOwner().OverrideNpcType(CreatureType.PEACE);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 20919)
        {
            Spawn(GetNpcId() + 1, 466.7468f, 514.5500f, 417.4044f, (sbyte)0); // Dragon Tiamat
            AIActions.DeleteOwner(this);
        }
    }
}
