using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionConstructAI (Tibald, Estrayl).</summary>
[AIName("eternal_bastion_construct")]
public class EternalBastionConstructAI : NpcAI
{
    private long lastMsgTime;

    public EternalBastionConstructAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleAttack(Creature creature)
    {
        if (GetNpcId() == 831333 || GetNpcId() == 831335)
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastMsgTime > 30000)
            {
                lastMsgTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                BroadcastMsg(GetNpcId() == 831333 ? SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_Notice_03() : SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_Notice_01());
            }
        }
    }

    private void BroadcastMsg(SM_SYSTEM_MESSAGE msg)
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), msg);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
