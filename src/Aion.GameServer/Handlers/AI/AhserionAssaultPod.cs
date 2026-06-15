using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionAssaultPod (Estrayl).</summary>
[AIName("ahserion_assault_pod")]
public class AhserionAssaultPod : GeneralNpcAI
{
    public AhserionAssaultPod(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { Activate(); return ValueTask.CompletedTask; }, 400L);
    }

    private void Activate()
    {
        WorldPosition p = GetPosition();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20776, 1, GetOwner()).UseWithoutPropSkill(); // Trooper Shock

        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnDefenders(p); return ValueTask.CompletedTask; }, 4000L);

        ThreadPoolManager.GetInstance().Schedule(_ => { AIActions.DeleteOwner(this); return ValueTask.CompletedTask; }, 6000L);
    }

    private void SpawnDefenders(WorldPosition p)
    {
        Spawn(297191, p.GetX() + 3, p.GetY() - 3, p.GetZ() + 3, (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
        Spawn(297191, p.GetX(), p.GetY(), p.GetZ() + 3, (sbyte)p.GetHeading()); // Ahserion Troopers Sorcerer
        Spawn(297191, p.GetX() + 3, p.GetY() + 3, p.GetZ() + 3, (sbyte)p.GetHeading()); // Ahserion Troopers Assassin
    }
}
