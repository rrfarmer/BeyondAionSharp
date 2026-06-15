using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/danuarReliquary/MaliciousIceStormAI (@author Ritsu, Estrayl, Yeats).</summary>
[AIName("malicious_ice_storm")]
public class MaliciousIceStormAI : NpcAI
{
    public MaliciousIceStormAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.UseSkill(this, 21180);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 1000L);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * 0.5f;
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21180)
            GetOwner().GetController().Delete();
    }
}
