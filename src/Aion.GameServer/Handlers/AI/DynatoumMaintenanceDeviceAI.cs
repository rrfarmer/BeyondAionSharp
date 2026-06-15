using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/DynatoumMaintenanceDeviceAI (@author M.O.G. Dision, Estrayl).</summary>
[AIName("dynatoum_maintenance_device")]
public class DynatoumMaintenanceDeviceAI : NpcAI
{
    private ScheduledTask skillTask;

    public DynatoumMaintenanceDeviceAI(Npc owner)
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
        Npc dynatoum = GetPosition().GetWorldMapInstance().GetNpc(GetPosition().GetMapId() == 301230000 ? 233740 : 234686);
        AIActions.TargetCreature(this, dynatoum);
        ScheduleSkillTask();
    }

    private void ScheduleSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!IsDead())
            {
                AIActions.UseSkill(this, 21535);
            }
        }, 10000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21535)
        {
            ScheduleSkillTask();
        }
    }

    protected override void HandleDespawned()
    {
        skillTask.Cancel(true);
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
