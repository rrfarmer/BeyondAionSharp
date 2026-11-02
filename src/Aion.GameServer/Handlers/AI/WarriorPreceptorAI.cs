using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/WarriorPreceptorAI (@author Luzien, w4terbomb).</summary>
[AIName("warrior_preceptor")]
public class WarriorPreceptorAI : CrucibleBossAI
{
    public WarriorPreceptorAI(Npc owner) : base(owner)
    {
    }

    private bool IsLight()
    {
        return GetNpcId() == 217578;
    }

    protected override void OnFightStart()
    {
        Say(IsLight() ? 1500207 : 1500209);
        Cast(19576, 10);
        Cast(19577, 10);
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 30, 40))
            Cast(19595, 10, NpcSkillTargetAttribute.RANDOM);
        if (Every(seconds, 37, 40))
            Cast(19596, 15, NpcSkillTargetAttribute.ME);
        if (Every(seconds, 10, 15))
            Cast(19598, 15, NpcSkillTargetAttribute.ME);
        if (seconds % 10 == 0)
        {
            if (Passed(80) || Passed(40))
                Cast(19597, 10);
            if (Passed(60) || Passed(20))
                Cast(19599, 10, NpcSkillTargetAttribute.ME);
        }
    }

    protected override void HandleDied()
    {
        Say(IsLight() ? 1500208 : 1500210);
        base.HandleDied();
    }
}
