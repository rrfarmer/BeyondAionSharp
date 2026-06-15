using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("twin_protector")]
public class TwinProtectorAI : AggressiveNoLootNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(65, 40, 25, 15, 10);
    private readonly List<Npc> adds = new List<Npc>();

    public TwinProtectorAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 65:
            case 40:
            case 15:
                GetOwner().ClearQueuedSkills();
                GetOwner().QueueSkill(21644, 1, 10000); // Raging Hellfire
                break;
            case 25:
            case 10:
                SpawnAdds(GetNpcId() % 2 == 0 ? 855622 : 855621, 20);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21644) // Raging Hellfire
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21645, 1, GetTarget()).UseNoAnimationSkill();
            SpawnAdds(855625, 50);
        }
    }

    private void SpawnAdds(int npcId, int hpThreshold)
    {
        int count = GetLifeStats().GetHpPercentage() < hpThreshold ? 3 : 1;
        foreach (var target in GetAggroList().StreamValidTargets(20).Take(count))
            adds.Add((Npc)Spawn(npcId, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0));
    }

    private void DespawnAdds()
    {
        foreach (Npc npc in adds)
            if (npc != null)
                npc.GetController().Delete();
    }

    protected override void HandleDespawned()
    {
        DespawnAdds();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        DespawnAdds();
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
