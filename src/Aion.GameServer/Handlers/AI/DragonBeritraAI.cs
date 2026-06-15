using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/DragonBeritraAI (Estrayl).</summary>
[AIName("drakenspire_dragon_beritra")]
public class DragonBeritraAI : AggressiveNoLootNpcAI
{
    private ScheduledTask authorityTask;

    public DragonBeritraAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Part of the de-spawn sequence:
    /// => Spawn soul extinction fields on all players in descending order i.e., starting with the player, which dealt the most damage.
    /// </summary>
    private void Wipe()
    {
        foreach (AggroInfo ai in GetAggroList().Stream()
            .Where(ai => ai.GetAttacker() is Player)
            .OrderByDescending(ai => ai.GetDamage()))
        {
            Spawn(855450, ai.GetAttacker().GetX(), ai.GetAttacker().GetY(), ai.GetAttacker().GetZ(), (sbyte)0);
        }
        // STR_CHAT_IDSeal_Vritra_Human_Gossip_09
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501276);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.CONSIDER_BOUNDS_IN_CAN_SEE_CHECK_WHEN_ATTACKED or AIQuestion.CONSIDER_BOUNDS_IN_CAN_SEE_CHECK_WHEN_ATTACKING => true,
            _ => base.Ask(question),
        };
    }
}
