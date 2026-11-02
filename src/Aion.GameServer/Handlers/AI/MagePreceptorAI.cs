using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/MagePreceptorAI (@author Luzien, w4terbomb).
/// The 60 and 30 thresholds are retail's, from pattern IDArena_S7_Named_3; upstream 1af948baf adopted them in place of its own 75/50/25.
/// See docs/retail-ai-fidelity.md.
/// </summary>
[AIName("mage_preceptor")]
public class MagePreceptorAI : CrucibleBossAI
{
    public MagePreceptorAI(Npc owner) : base(owner)
    {
    }

    private bool IsLight()
    {
        return GetNpcId() == 217580;
    }

    protected override void OnFightStart()
    {
        Say(IsLight() ? 1500215 : 1500217);
        Cast(19607, 10);
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 15, 40))
            Cast(19605, 10);
        if (Every(seconds, 12, 12))
            Cast(19581, 10);
        if (Passed(60))
        {
            Cast(19606, 10);
            Cast(19609, 10, NpcSkillTargetAttribute.ME);
            WorldPosition p = GetPosition();
            Spawn(282363, p.GetX() + 5, p.GetY() - 5, p.GetZ(), (sbyte)p.GetHeading()); // S7_Summon_MagmaElemental_55_Ah
            Spawn(282364, p.GetX() - 5, p.GetY() + 5, p.GetZ(), (sbyte)p.GetHeading()); // S7_Summon_TempestElemental_55_Ah
            Cast(19608, 10, NpcSkillTargetAttribute.ME);
        }
        if (Passed(30))
        {
            Cast(19606, 10);
            Cast(19605, 10, NpcSkillTargetAttribute.RANDOM);
            Cast(19605, 10, NpcSkillTargetAttribute.RANDOM);
            Cast(19582, 10);
        }
    }

    protected override void OnFightEnd()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(282363, 282364))
            npc.GetController().Delete();
    }

    protected override void HandleDied()
    {
        Say(IsLight() ? 1500216 : 1500218);
        base.HandleDied();
    }
}
