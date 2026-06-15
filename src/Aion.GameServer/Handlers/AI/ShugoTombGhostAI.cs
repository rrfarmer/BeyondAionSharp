using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/shugoImperialTomb/ShugoTombGhostAI (@author Ritsu).</summary>
[AIName("shugo_tomb_ghost")]
public class ShugoTombGhostAI : AggressiveNpcAI
{
    public ShugoTombGhostAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        GetOwner().SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.ALLOW_DECAY => GetNpcId() switch
            {
                219505 or 219506 or 219507 => false,
                _ => true,
            },
            _ => base.Ask(question),
        };
    }
}
