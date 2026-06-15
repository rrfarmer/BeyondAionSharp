using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/CalindiSummonsAI (Cheatkiller, Luzien, Estrayl).</summary>
[AIName("calindi_summon")]
public class CalindiSummonsAI : NpcAI
{
    private ScheduledTask task;
    private VisibleObject textureObject = null;

    public CalindiSummonsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int delay = GetDelay();
        if (GetFakeTextureNpcId() != 0)
        {
            textureObject = Spawn(GetFakeTextureNpcId(), GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0);
        }

        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            AIActions.UseSkill(this, GetSkillId());
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(500), System.TimeSpan.FromMilliseconds(delay));

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            if (textureObject != null)
            {
                textureObject.GetController().Delete();
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(15000));
    }

    private int GetSkillId()
    {
        switch (GetNpcId())
        {
            case 283131:
                return 20916;
            case 283133:
                return 20914;
            case 856298:
                return 21891;
            case 856299:
                return 21892;
            default:
                return 0;
        }
    }

    private int GetFakeTextureNpcId()
    {
        switch (GetNpcId())
        {
            case 283131:
            case 856299:
                return 283130;
            case 283133:
            case 856298:
                return 283132;
            default:
                return 0;
        }
    }

    private int GetDelay()
    {
        switch (GetNpcId())
        {
            case 283131:
            case 856299:
                return 2000;
            case 283133:
            case 856298:
                return 500;
            default:
                return 0;
        }
    }

    protected override void HandleDespawned()
    {
        task.Cancel(true);
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
