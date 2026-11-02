using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/ManduriGiantAI.
/// Golden eye mantutu of stage 5: every 40 seconds it grows hungry or thirsty. The feed and water supply devices, pulled by two players at once,
/// put out feed or water that it goes after; eating the right one removes its buff, the wrong one or one killed by players makes it crave even more.
/// </summary>
[AIName("empyrean_manduri_giant")]
public class ManduriGiantAI : CrucibleBossAI
{
    public const int FEED = 281128, WATER = 281129;
    private volatile bool hungry, thirsty;
    private bool lowHp, midHp, highHp;

    public ManduriGiantAI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        hungry = thirsty = false;
        lowHp = midHp = highHp = false;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 5, 40))
        {
            bool hunger = Rnd.NextBoolean();
            Cast(hunger ? 18180 : 18181, 36, NpcSkillTargetAttribute.ME); // Hunger / Thirst
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (hunger)
                    hungry = true;
                else
                    thirsty = true;
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(1000));
        }
        if (Every(seconds, 5, 5))
        {
            if (hp < 25 && !lowHp)
            {
                lowHp = true;
                Cast(18185, 36);
                Cast(18186, 36);
            }
            else if (hp >= 26 && hp <= 50 && !midHp)
            {
                midHp = true;
                Cast(18187, 36);
            }
            else if (hp >= 51 && hp <= 75 && !highHp)
            {
                highHp = true;
                Cast(18184, 36);
            }
        }
        if (Every(seconds, 20, 20))
        {
            if (hp < 50 && Rnd.NextBoolean())
            {
                Cast(18187, 36);
            }
            else if (hp < 25)
            {
                Cast(18185, 36);
                Cast(18186, 36);
            }
            else if (hp <= 50)
            {
                Cast(18184, 36);
            }
            else if (hp <= 75)
            {
                Cast(Rnd.NextBoolean() ? 18188 : 18184, 36);
            }
            else
            {
                Cast(18188, 36);
            }
        }
    }

    /// <summary>The giant goes after feed or water as soon as it appears.</summary>
    public void OnFoodSpawned(Npc food)
    {
        if (IsInFight())
            GetAggroList().AddHate(food, 5000000);
    }

    public void OnFoodDied(int foodNpcId, bool eatenByGiant)
    {
        if (IsDead())
            return;
        if (eatenByGiant && foodNpcId == FEED && hungry)
        {
            hungry = false;
            CastOnSelf(18183, 36); // Satiation
        }
        else if (eatenByGiant && foodNpcId == WATER && thirsty)
        {
            thirsty = false;
            CastOnSelf(18561, 36); // Quench Thirst
        }
        else
        {
            CastOnSelf(18182, 36); // Craving
        }
    }
}
