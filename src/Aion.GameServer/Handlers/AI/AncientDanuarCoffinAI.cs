using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/danuarSanctuary/AncientDanuarCoffinAI (Tibald).</summary>
[AIName("ancientdanuarcoffin")]
public class AncientDanuarCoffinAI : GeneralNpcAI
{
    public AncientDanuarCoffinAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 1;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (Rnd.Chance() < 40)
        {
            Spawn(233085, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
        }
    }
}
