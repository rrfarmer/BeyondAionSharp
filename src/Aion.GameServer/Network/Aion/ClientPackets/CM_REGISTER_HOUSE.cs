using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REGISTER_HOUSE (Rolandas). Registers the player's house for auction with a starting bid (charges a registration fee). HousingBidService red-tolerated.</summary>
public class CM_REGISTER_HOUSE : AionClientPacket
{
    private long bidKinah;

    public CM_REGISTER_HOUSE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        bidKinah = ReadQ();
        ReadQ(); // always 100000
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (!HousingBidService.GetInstance().IsRegisteringAllowed())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_TIMEOUT());
            return;
        }

        House house = player.GetActiveHouse();
        if (house == null || house.GetHouseType() == HouseType.STUDIO)
            return; // should not happen

        if (house.GetBids() != null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL_ALREADY_REGISTED());
            return;
        }

        if (!house.IsFeePaid() && HousingConfig.ENABLE_HOUSE_PAY)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_OVERDUE());
            return;
        }

        long fee = (long)(bidKinah * HousingConfig.AUCTION_REGISTRATION_FEE_PERCENT);

        if (!player.GetInventory().TryDecreaseKinah(fee))
        {
            // client has it's own validation, so we only get here if AUCTION_REGISTRATION_FEE_PERCENT is higher than the default in the client (30%)
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(fee));
            return;
        }
        if (HousingBidService.GetInstance().Auction(house, bidKinah))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_MY_HOUSE(house.GetAddress().GetId()));
            SendPacket(new SM_RECEIVE_BIDS(0));
        }
        else
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_TIMEOUT());
            player.GetInventory().IncreaseKinah(fee);
        }
    }
}
