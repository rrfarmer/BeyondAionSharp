using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GET_HOUSE_BIDS (Rolandas). Sends the current house-auction bid list, split across packets. HousingBidService/SplitList/SM_HOUSE_BIDS red-tolerated.</summary>
public class CM_GET_HOUSE_BIDS : AionClientPacket
{
    public CM_GET_HOUSE_BIDS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {

    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        List<HouseBids> houseBids = HousingBidService.GetInstance().GetBidInfo(player.GetRace());
        SplitList<HouseBids> bidsSplitList = new DynamicServerPacketBodySplitList<HouseBids>(houseBids, true, SM_HOUSE_BIDS.STATIC_BODY_SIZE,
            SM_HOUSE_BIDS.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
        foreach (var part in bidsSplitList)
            PacketSendUtility.SendPacket(player, new SM_HOUSE_BIDS(part.IsFirst(), part.IsLast(), part));
    }
}
