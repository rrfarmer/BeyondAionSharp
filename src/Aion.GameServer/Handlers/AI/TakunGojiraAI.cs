using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/TakunGojiraAI (@author Luzien).
/// Takun and Gojira of stage 9 fight each other as well. After three minutes of fighting both fly into a rage, and from 70 seconds on the
/// arena keeps drawing their attention to the players in it.
/// </summary>
[AIName("takun_gojira")]
public class TakunGojiraAI : CrucibleBossAI
{
    private const int TAKUN = 217596;
    private int attackIn, rageAttackIn, attentionIn;

    public TakunGojiraAI(Npc owner) : base(owner)
    {
    }

    private bool IsTakun()
    {
        return GetNpcId() == TAKUN;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Npc counterpart = GetPosition().GetWorldMapInstance().GetNpc(IsTakun() ? 217597 : TAKUN);
            if (counterpart != null)
                GetAggroList().AddHate(counterpart, 1000000);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(500));
    }

    protected override void OnFightStart()
    {
        if (IsTakun())
            Cast(17477, 38);
        else
            Cast(16828, 38, NpcSkillTargetAttribute.ME);
        attackIn = IsTakun() ? 8 : 18;
        rageAttackIn = 0;
        attentionIn = 70;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (--attackIn <= 0)
        {
            if (IsTakun())
            {
                int roll = Rnd.Get(1, 100);
                if (roll <= 15)
                {
                    Cast(19625, 38);
                    attackIn = 25;
                }
                else if (roll <= 24) // 10 % of the remaining 85 %
                {
                    Cast(16859, 38);
                    attackIn = 25;
                }
                else
                {
                    Cast(16859, 38);
                    attackIn = 15;
                }
            }
            else
            {
                int roll = Rnd.Get(1, 100);
                if (roll <= 30)
                {
                    Cast(19626, 1);
                    attackIn = 30;
                }
                else if (roll <= 37) // 10 % of the remaining 70 %
                {
                    Cast(19626, 1);
                    attackIn = 40;
                }
                else
                {
                    Cast(17968, 38);
                    Cast(17981, 38);
                    attackIn = 30;
                }
            }
        }
        if (seconds == 180)
        {
            Cast(19624, 10, NpcSkillTargetAttribute.ME);
            if (Attacker(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET) is Creature target)
                GetAggroList().AddHate(target, IsTakun() ? 1000000 : 10000000);
            rageAttackIn = 8;
        }
        if (rageAttackIn > 0 && --rageAttackIn == 0)
        {
            if (IsTakun())
            {
                Cast(16859, 38);
                Cast(16859, 38);
            }
            else
            {
                Cast(17968, 38);
                Cast(17981, 38);
            }
            rageAttackIn = 10;
        }
        if (--attentionIn <= 0)
        {
            GetKnownList().ForEachPlayer(player =>
            {
                if (!player.IsDead() && PositionUtil.IsInRange(GetOwner(), player, 100))
                    GetAggroList().AddHate(player, 100);
            });
            attentionIn = seconds < 170 ? 100 : 30;
        }
    }
}
