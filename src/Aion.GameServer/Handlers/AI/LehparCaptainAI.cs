using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/LehparCaptainAI.
/// Lehpar boss of stage 5: calls a pair of lehpars for help every 90 seconds while above 70 % HP. Below that, the call comes once and then no
/// more, its set depending on the HP.
/// </summary>
[AIName("lehpar_captain")]
public class LehparCaptainAI : CrucibleBossAI
{
    private const int FIGHTER = 217531, ASSASSIN = 217533, WIZARD = 217534, PRIEST = 217536;
    private readonly List<Npc> helpers = new List<Npc>();
    private int helpIn;

    public LehparCaptainAI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        Cast(16519, 34);
        helpIn = 60;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 20, 20))
        {
            if (hp > 70)
            {
                Cast(16519, 34);
                Cast(16628, 33, NpcSkillTargetAttribute.RANDOM);
            }
            else if (hp > 30)
            {
                Cast(16997, 34);
            }
            else
            {
                Cast(16519, 34);
                Cast(16684, 33);
            }
        }
        if (helpIn > 0 && --helpIn == 0)
        {
            if (hp > 70)
            {
                CallHelp(FIGHTER, ASSASSIN);
                helpIn = 90;
            }
            else if (hp > 30)
            {
                CallHelp(ASSASSIN, WIZARD);
            }
            else
            {
                CallHelp(ASSASSIN, PRIEST, WIZARD);
            }
        }
    }

    private void CallHelp(params int[] npcIds)
    {
        foreach (int npcId in npcIds)
        {
            if (RndSpawnInRange(npcId, 1, 5) is Npc npc)
                lock (helpers)
                {
                    helpers.Add(npc);
                }
        }
    }

    protected override void OnFightEnd()
    {
        lock (helpers)
        {
            foreach (Npc npc in helpers.Where(npc => !npc.IsDead()).ToList())
                npc.GetController().Delete();
            helpers.Clear();
        }
    }
}
