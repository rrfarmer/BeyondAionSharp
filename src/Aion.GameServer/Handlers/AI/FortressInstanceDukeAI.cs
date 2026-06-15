using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyss/FortressInstanceDukeAI (@author Estrayl, since AION 4.8).</summary>
[AIName("fortress_instance_duke")]
public class FortressInstanceDukeAI : AggressiveNpcAI
{
    public FortressInstanceDukeAI(Npc owner)
        : base(owner)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 18003)
            Spawn(284978, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)GetOwner().GetHeading());
    }

    private void DeleteSummons()
    {
        GetPosition().GetWorldMapInstance().ForEachNpc(npc =>
        {
            if (npc.GetNpcId() >= 284978 && npc.GetNpcId() <= 284981)
                npc.GetController().Delete();
        });
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        DeleteSummons();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        DeleteSummons();
    }
}
