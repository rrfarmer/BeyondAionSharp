using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/KingConsierdAI (@author Luzien, w4terbomb).
/// Retail pattern <c>IDArena_S9_Named_2</c>. Upstream 1af948baf rewrote this onto the fight clock and arrived at the same retail facts this
/// port had corrected by hand: the 80/55/25 bands, two condors every thirty seconds with <c>spawn_range=10</c> and nothing below 25 %, and
/// their ten-minute life. See docs/retail-ai-fidelity.md.
/// </summary>
[AIName("king_consierd")]
public class KingConsierdAI : CrucibleBossAI
{
    private const int BABY_CONSIERD = 282378; // IDArena_Summon_Condor_55_An
    private int cycleIn, frenzyStep;

    public KingConsierdAI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        Cast(19691, 1);
        Cast(17954, 29);
        cycleIn = 25;
        frenzyStep = 0;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (seconds == 240)
            Cast(19624, 10, NpcSkillTargetAttribute.ME);
        if (Passed(80))
        {
            StunAndTurn();
            cycleIn = 25;
        }
        else if (Passed(55))
        {
            // Retail only arms BTIMERI_INDEX_4 here, so the first wave lands thirty seconds later; upstream 1af948baf
            // also calls one at the crossing. The maintainer kept retail (2026-11-02), see docs/retail-ai-fidelity.md.
            cycleIn = 30;
        }
        else if (Passed(25))
        {
            Cast(19690, 1);
            SwitchTarget(WeakestAttacker());
            cycleIn = 12;
            frenzyStep = 0;
        }
        else if (--cycleIn <= 0)
        {
            if (hp > 80)
            {
                Cast(17950, 29);
                cycleIn = 20;
            }
            else if (hp > 55)
            {
                StunAndTurn();
                cycleIn = 25;
            }
            else if (hp > 25)
            {
                CallBabies();
                cycleIn = 30;
            }
            else
            {
                if (frenzyStep % 2 == 0)
                {
                    Cast(17957, 29);
                    Cast(17954, 29);
                }
                else
                {
                    Cast(19691, 1);
                    Cast(17956, 29);
                    SwitchTarget(WeakestAttacker());
                    Cast(17956, 29);
                }
                frenzyStep++;
                cycleIn = frenzyStep % 2 == 1 ? 25 : 20; // after 12 s the two steps alternate 25 s and 20 s apart
            }
        }
    }

    private void StunAndTurn()
    {
        Cast(17951, 29);
        SwitchTarget(Attacker(AggroTarget.THIRD_MOST_HATED));
        Cast(17952, 29);
        Cast(17954, 29);
    }

    private void CallBabies()
    {
        Cast(17951, 29);
        for (int i = 0; i < 2; i++)
        {
            if (RndSpawnInRange(BABY_CONSIERD, 1, 10) is Npc baby)
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    baby.GetController().DeleteIfAliveOrCancelRespawn();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(600000));
        }
        SwitchTarget(Attacker(AggroTarget.THIRD_MOST_HATED));
        Cast(17952, 29);
    }

    protected override void OnFightEnd()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(BABY_CONSIERD))
            npc.GetController().Delete();
        if (!IsDead())
            CastOnSelf(19160, 1);
    }
}
