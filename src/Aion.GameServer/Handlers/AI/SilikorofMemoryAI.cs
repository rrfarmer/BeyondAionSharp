using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theobomosLab/SilikorofMemoryAI (@author Ritsu).</summary>
[AIName("silikor")]
public class SilikorofMemoryAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50, 25, 10);

    public SilikorofMemoryAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        Sp(281054);
        Sp(281053);
    }

    private void Sp(int npcId)
    {
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        int distance = Rnd.Get(0, 2);
        float x1 = (float)(Math.Cos(angleRadians) * distance);
        float y1 = (float)(Math.Sin(angleRadians) * distance);
        WorldPosition p = GetPosition();
        Spawn(npcId, p.GetX() + x1, p.GetY() + y1, p.GetZ(), (sbyte)p.GetHeading());
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetNpcId() == 214668)
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18481, 1, GetOwner()).UseSkill();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDied()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(281054, 281053))
            npc.GetController().Delete();
        base.HandleDied();
    }
}
