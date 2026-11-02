using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/QueenAlukinaAI (@author Luzien, w4terbomb).
/// Retail pattern <c>IDArena_S8_Named_3</c>. Upstream 1af948baf rewrote this onto the fight clock and kept the retail facts this port had
/// corrected by hand: the 80/55/25 bands and the blobbles' thirty seconds. See docs/retail-ai-fidelity.md.
/// </summary>
[AIName("alukina_emp")]
public class QueenAlukinaAI : CrucibleBossAI
{
    private const int FIRST_PIERCE = 12, PIERCE_WITH_SHOUT = 25, PIERCE_QUIET = 9;
    private int cycleIn, pierceStep;

    public QueenAlukinaAI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        Cast(19687, 1);
        Say(341124);
        Cast(17928, 41);
        cycleIn = 18;
        pierceStep = 0;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Passed(80))
        {
            Cast(19687, 1);
            Say(341125);
            Cast(17899, 41);
            Cast(17900, 41);
            SwitchTarget(RandomAttacker());
            cycleIn = 25;
        }
        else if (Passed(55))
        {
            Cast(17899, 41);
            Cast(19687, 1);
            Say(341126);
            Cast(17280, 41);
            SwitchTarget(RandomAttacker());
            cycleIn = FIRST_PIERCE;
            pierceStep = 0;
        }
        else if (Passed(25))
        {
            Cast(17899, 41);
            Cast(17901, 41);
            Cast(17902, 41, NpcSkillTargetAttribute.RANDOM);
            SwitchTarget(WeakestAttacker());
            Cast(17902, 41);
            cycleIn = 20;
        }
        else if (--cycleIn <= 0)
        {
            if (hp > 80)
            {
                Cast(17928, 41);
                cycleIn = 18;
            }
            else if (hp > 55)
            {
                Say(340487);
                Cast(17900, 41);
                Cast(17900, 41);
                SwitchTarget(WeakestAttacker());
                Cast(17899, 41);
                cycleIn = 25;
            }
            else if (hp > 25)
            {
                bool shout = pierceStep % 2 == 1; // after 12 s the pierces alternate 25 s (with the shout) and 9 s
                if (shout)
                    Say(341126);
                Cast(17280, 41);
                SwitchTarget(RandomAttacker());
                pierceStep++;
                cycleIn = pierceStep % 2 == 1 ? PIERCE_WITH_SHOUT : PIERCE_QUIET;
            }
            else
            {
                Cast(17901, 41);
                Cast(17902, 41);
                Cast(17902, 41);
                Cast(17902, 41);
                cycleIn = 20;
            }
        }
    }

    protected override void HandleDied()
    {
        WorldPosition p = GetPosition();
        for (int i = 0; i < 7; i++)
        {
            if (Spawn(280713, p.GetX() + Rnd.Get(-5, 5), p.GetY() + Rnd.Get(-5, 5), p.GetZ(), (sbyte)p.GetHeading()) is Npc spawnling)
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    spawnling.GetController().DeleteIfAliveOrCancelRespawn();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(30000));
        }
        base.HandleDied();
    }
}
