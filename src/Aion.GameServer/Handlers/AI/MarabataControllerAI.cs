using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/MarabataControllerAI (@author xTz).</summary>
[AIName("marabata_controller")]
public class MarabataControllerAI : NpcAI
{
    public MarabataControllerAI(Npc owner)
        : base(owner)
    {
    }

    private Npc GetBoss()
    {
        Npc npc = GetNpcId() switch
        {
            700443 or 700444 or 700442 => GetPosition().GetWorldMapInstance().GetNpc(214850),
            700446 or 700447 or 700445 => GetPosition().GetWorldMapInstance().GetNpc(214851),
            700440 or 700441 or 700439 => GetPosition().GetWorldMapInstance().GetNpc(214849),
            _ => null,
        };
        return npc;
    }

    private void ApplyEffect(bool remove)
    {
        Npc boss = GetBoss();
        if (boss != null && !boss.IsDead())
        {
            switch (GetNpcId())
            {
                case 700443:
                case 700446:
                case 700440:
                    if (remove)
                        boss.GetEffectController().RemoveEffect(18556);
                    else
                        boss.GetController().UseSkill(18556);
                    break;
                case 700444:
                case 700447:
                case 700441:
                    // TODO unk
                    break;
                case 700442:
                case 700445:
                case 700439:
                    if (remove)
                        boss.GetEffectController().RemoveEffect(18110);
                    else
                        boss.GetController().UseSkill(18110);
                    break;
            }
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        ApplyEffect(true);
        AIActions.DeleteOwner(this);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            UseSkill();
            return ValueTask.CompletedTask;
        }, 3500L);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            ApplyEffect(false);
            return ValueTask.CompletedTask;
        }, 10000L);
    }

    private void UseSkill()
    {
        if (IsDead())
            return;

        AIActions.TargetSelf(this);
        int skill = GetNpcId() switch
        {
            700443 or 700446 or 700440 => 18554,
            700444 or 700447 or 700441 => 18555,
            700442 or 700445 or 700439 => 18553,
            _ => 0,
        };
        AIActions.UseSkill(this, skill);
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_DECAY:
            case AIQuestion.ALLOW_RESPAWN:
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
