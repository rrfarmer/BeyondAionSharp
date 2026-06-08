using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/ItemUseObserver (MrPoke).
/// </summary>
public abstract class ItemUseObserver : ActionObserver
{
    protected ItemUseObserver()
        : base(ObserverType.ALL)
    {
    }

    public sealed override void Attack(Creature creature, int skillId)
    {
        Abort();
    }

    public sealed override void Attacked(Creature creature, int skillId)
    {
        Abort();
    }

    public sealed override void Died(Creature creature)
    {
        Abort();
    }

    public sealed override void Dotattacked(Creature creature, Effect dotEffect)
    {
        Abort();
    }

    public sealed override void Equip(Item item, Player owner)
    {
        Abort();
    }

    public sealed override void Unequip(Item item, Player owner)
    {
        Abort();
    }

    public sealed override void Moved()
    {
        Abort();
    }

    public sealed override void StartSkillCast(Skill skill)
    {
        Abort();
    }

    public sealed override void Sit()
    {
        Abort();
    }

    public override void EndSkillCast(Skill skill)
    {
        Abort();
    }

    public override void Itemused(Item item)
    {
        Abort();
    }

    public override void BoostSkillCost(Skill skill)
    {
        Abort();
    }

    public abstract void Abort();
}
