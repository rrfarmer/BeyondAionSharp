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

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (isHome.CompareAndSet(true, false))
        {
            WorldPosition p = GetPosition();
            Npc smoke = (Npc)Spawn(282465, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
            smoke.GetController().Delete();
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
