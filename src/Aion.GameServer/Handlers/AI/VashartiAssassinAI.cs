using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("vasharti_assassin")]
public class VashartiAssassinAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);

    public VashartiAssassinAI(Npc owner) : base(owner)
    {
    }

    /// <summary>
    /// Retail <c>IDElemental_Smoke</c> stands for six seconds.
    /// </summary>
    /// <remarks>
    /// <b>It was spawned and deleted on the following line</b>, so the smoke this assassin vanishes into
    /// never appeared at all. That reads as a bounded spawn to any audit and to any reviewer; only
    /// retail's own six seconds shows it was meant to be seen.
    /// </remarks>
    private const int SmokeLife = 6;

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (isHome.CompareAndSet(true, false))
        {
            WorldPosition p = GetPosition();
            SpawnFor(282465, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading(), SmokeLife);
        }
        base.HandleCreatureAggro(creature);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead())
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19915, 60, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, 2000L);
    }

    protected override void HandleBackHome()
    {
        isHome.Set(true);
        base.HandleBackHome();
        GetEffectController().RemoveEffect(19915);
        GetEffectController().RemoveEffect(19916);
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19915, 60, GetOwner()).UseNoAnimationSkill();
    }
}
