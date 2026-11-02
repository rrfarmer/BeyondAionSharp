using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/RM1337AI (@author Luzien, w4terbomb).
/// Retail pattern <c>IDArena_S8_Named_6</c> (217593). Upstream 1af948baf rewrote this onto the fight clock and arrived at the retail facts
/// this port had corrected by hand: both rungs change pace at half health, and the fire is a fixed shape rather than a roll. See
/// docs/retail-ai-fidelity.md.
/// </summary>
[AIName("rm_1337")]
public class RM1337AI : CrucibleBossAI
{
    private const int FIRE = 282373; // S8_Summon_Fire_55_Ae
    private int attackIn, followUpIn, sparksIn;

    public RM1337AI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        Say(1500229);
        attackIn = 10;
        followUpIn = 0;
        sparksIn = 30;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (--attackIn <= 0)
        {
            if (hp > 50)
            {
                Cast(19549, 10);
                attackIn = 15;
            }
            else
            {
                Cast(19552, 10, NpcSkillTargetAttribute.RANDOM_EXCEPT_CURRENT_TARGET);
                Cast(19553, 10);
                attackIn = 18;
            }
            followUpIn = 8;
        }
        if (followUpIn > 0 && --followUpIn == 0)
        {
            Cast(19550, 10, NpcSkillTargetAttribute.RANDOM);
            RndSpawnInRange(FIRE, 0, 2);
        }
        if (--sparksIn <= 0)
        {
            bool enraged = hp <= 50;
            if (enraged)
                Cast(19550, 10);
            Say(1500230);
            Cast(19551, 10);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SpawnSparks(enraged);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(5000));
            sparksIn = enraged ? 50 : 60;
        }
    }

    private void SpawnSparks(bool enraged)
    {
        if (IsDead())
            return;
        for (int i = 0; i < (enraged ? 8 : 4); i++)
            RndSpawnInRange(FIRE, 0, 5);
        for (int i = 0; i < (enraged ? 10 : 5); i++)
            RndSpawnInRange(FIRE, 5, 15);
        if (enraged)
        {
            for (int i = 0; i < 5; i++)
            {
                if (RandomAttacker() is Creature target)
                    Spawn(FIRE, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0);
            }
        }
    }

    protected override void HandleDied()
    {
        Say(1500231);
        base.HandleDied();
    }
}
