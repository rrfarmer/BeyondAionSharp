using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Reward;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PLAYER_LISTENER (ginho1). Sent every five minutes; pushes available web rewards if enabled. WebRewardService red-tolerated.</summary>
public class CM_PLAYER_LISTENER : AionClientPacket
{
    public CM_PLAYER_LISTENER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        if (GSConfig.ENABLE_WEB_REWARDS)
            WebRewardService.GetInstance().SendAvailableRewards(GetConnection().GetActivePlayer());
    }
}
