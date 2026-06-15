using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/DynatoumHealerAI (@author Estrayl).</summary>
[AIName("dynatoum_healer")]
public class DynatoumHealerAI : GeneralNpcAI
{
    public DynatoumHealerAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartHealing();
    }

    private void StartHealing()
    {
        Npc boss = GetPosition().GetWorldMapInstance().GetNpc(GetPosition().GetMapId() == 301230000 ? 233740 : 234686);
        if (boss != null)
        {
            AIActions.TargetCreature(this, boss);
            GetOwner().QueueSkill(21535, 1, 10000);
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21535)
        {
            GetOwner().QueueSkill(21535, 1, 10000);
        }
    }
}
