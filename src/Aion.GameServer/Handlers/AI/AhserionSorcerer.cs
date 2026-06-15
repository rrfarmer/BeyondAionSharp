using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionSorcerer (Estrayl).</summary>
[AIName("ahserion_sorcerer")]
public class AhserionSorcerer : AhserionAggressiveNpcAI
{
    public AhserionSorcerer(Npc owner)
        : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }
}
