using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/CalindiSurkanaAI (@author Cheatkiller, Estrayl).</summary>
[AIName("calindi_surkana")]
public class CalindiSurkanaAI : NpcAI
{
    private ScheduledTask reflectTask;
    private int calindiId;

    public CalindiSurkanaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        calindiId = GetPosition().GetMapId() == 300520000 ? 219359 : 236274;
        switch (GetNpcId())
        {
            case 730695:
            case 731629:
                StartReflectBuffTask(20590);
                break;
            case 730696:
            case 731630:
                StartReflectBuffTask(20591);
                break;
        }
    }

    private void StartReflectBuffTask(int buffId)
    {
        reflectTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            Npc calindi = GetPosition().GetWorldMapInstance().GetNpc(calindiId);
            if (IsDead() || calindi == null || calindi.IsDead())
            {
                reflectTask.Cancel(false);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(buffId, GetOwner(), GetPosition().GetWorldMapInstance().GetNpc(calindiId));
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(2500), System.TimeSpan.FromMilliseconds(5000));
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (reflectTask != null && !reflectTask.IsCancelled)
        {
            reflectTask.Cancel(false);
        }
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
