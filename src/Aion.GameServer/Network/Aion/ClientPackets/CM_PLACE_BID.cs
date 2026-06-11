using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PLACE_BID (Rolandas). Places a bid on a house auction. HousingBidService red-tolerated.</summary>
public class CM_PLACE_BID : AionClientPacket
{
    private int listIndex;
    private long bidOffer;

    public CM_PLACE_BID(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        listIndex = ReadD();
        bidOffer = ReadQ();
    }

    protected override void RunImpl()
    {
        if (HousingConfig.ENABLE_HOUSE_AUCTIONS)
        {
            Player player = GetConnection().GetActivePlayer();
            HousingBidService.GetInstance().Bid(player, listIndex, bidOffer);
        }
    }
}
