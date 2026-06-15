using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/NeutralGuardAI (Cheatkiller).</summary>
[AIName("neutralguard")]
public class NeutralGuardAI : AggressiveNpcAI
{
    public NeutralGuardAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        GetOwner().OverrideNpcType(CreatureType.SUPPORT);
    }

    protected override void CreatureNeedsHelp(Creature attacker)
    {
        if (PositionUtil.IsInRange(attacker, GetOwner(), 20) && GetOwner().GetType_(attacker) != CreatureType.AGGRESSIVE
            && attacker.GetTarget() is Player)
        {
            GetOwner().OverrideNpcType(CreatureType.AGGRESSIVE);
            GetOwner().GetAggroList().AddHate(attacker, 1000);
        }
    }
}
