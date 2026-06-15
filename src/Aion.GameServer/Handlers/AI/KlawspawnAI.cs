using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/heiron/KlawspawnAI (cheatkiller).</summary>
[AIName("klawspawn")]
public class KlawspawnAI : GeneralNpcAI
{
    public KlawspawnAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        Npc npc = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(212120);
        if (npc == null)
        {
            if (Rnd.Chance() < 10)
            {
                Spawn(212120, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
                AIActions.Die(this, creature);
            }
        }
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 1;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        AIActions.DeleteOwner(this);
    }
}
