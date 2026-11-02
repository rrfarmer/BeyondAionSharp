using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/RangerPreceptorAI (@author w4terbomb).</summary>
[AIName("ranger_preceptor")]
public class RangerPreceptorAI : CrucibleBossAI
{
    private int focusIn;

    public RangerPreceptorAI(Npc owner) : base(owner)
    {
    }

    private bool IsLight()
    {
        return GetNpcId() == 217579;
    }

    protected override void OnFightStart()
    {
        Say(IsLight() ? 1500211 : 1500213);
        focusIn = 30;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 10, 15))
        {
            if (Passed(60) || Passed(30))
                WindWalk();
            else
                Cast(19600, 10);
        }
        if (Every(seconds, 20, 40))
            Cast(19601, 15, NpcSkillTargetAttribute.RANDOM);
        if (hp < 50 && --focusIn <= 0)
        {
            Cast(19602, 10, NpcSkillTargetAttribute.ME);
            focusIn = 30;
        }
    }

    /// <summary>Runs about for 6 seconds and comes back at a random attacker.</summary>
    private void WindWalk()
    {
        CastOnSelf(19604, 1);
        CrucibleRun.Start(this, null, 6000, () =>
        {
            SwitchTarget(RandomAttacker());
            Cast(19579, 10);
            Cast(19578, 10);
        });
    }

    protected override void HandleDied()
    {
        Say(IsLight() ? 1500212 : 1500214);
        base.HandleDied();
    }
}
