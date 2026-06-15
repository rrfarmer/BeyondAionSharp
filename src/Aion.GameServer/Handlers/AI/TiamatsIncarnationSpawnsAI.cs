using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/TiamatsIncarnationSpawnsAI (@author Estrayl).</summary>
[AIName("tiamats_incarnation_spawn")]
public class TiamatsIncarnationSpawnsAI : NpcAI
{
    private ScheduledTask skillTask;

    public TiamatsIncarnationSpawnsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, GetSkillId());
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(2500), System.TimeSpan.FromMilliseconds(3000));
    }

    protected override void HandleDespawned()
    {
        skillTask.Cancel(true);
        base.HandleDespawned();
    }

    private int GetSkillId()
    {
        switch (GetNpcId())
        {
            case 282727: // Gravity Whirlpool
            case 856074:
                return 20155;
            case 282729: // Thunderbolt Whirlpool
            case 856076:
                return 20156;
            case 282731: // Petrification Crystal
            case 856072:
                return 20159;
            case 282735: // Cavity of Earth
            case 856068:
                return 20172;
            case 282737: // Collapsing Earth
            case 856070:
                return 20173;
            default:
                return 0;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}
