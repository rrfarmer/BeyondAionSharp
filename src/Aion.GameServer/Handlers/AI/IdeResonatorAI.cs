using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/infinityShard/IdeResonatorAI (@author Cheatkiller, Luzien, Estrayl).</summary>
[AIName("ide_resonator")]
public class IdeResonatorAI : NpcAI
{
    private ScheduledTask task;

    public IdeResonatorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Npc hyperion = GetPosition().GetWorldMapInstance().GetNpc(231073);
        AIActions.TargetCreature(this, hyperion);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && hyperion != null && !hyperion.IsDead())
            {
                int firstBuff = GetNpcId() switch
                {
                    231093 => 21381,
                    231094 => 21383,
                    _ => 21257, // NCSoft only implemented 3 different skill IDs for those temporary buffs
                };
                AIActions.UseSkill(this, firstBuff);
            }

            return ValueTask.CompletedTask;
        }, 8000L);

        task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && hyperion != null && !hyperion.IsDead())
            {
                int secondBuff = 0;
                if (!hyperion.GetEffectController().HasAbnormalEffect(21258))
                    secondBuff = 21258;
                else if (!hyperion.GetEffectController().HasAbnormalEffect(21382))
                    secondBuff = 21382;
                else if (!hyperion.GetEffectController().HasAbnormalEffect(21384))
                    secondBuff = 21384;
                else if (!hyperion.GetEffectController().HasAbnormalEffect(21416))
                    secondBuff = 21416;

                if (secondBuff != 0)
                    AIActions.UseSkill(this, secondBuff);
            }

            return ValueTask.CompletedTask;
        }, 18000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21258:
            case 21382:
            case 21384:
            case 21416:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21371, GetOwner(), GetOwner());
                break;
        }
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
            task.Cancel(true);
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
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
