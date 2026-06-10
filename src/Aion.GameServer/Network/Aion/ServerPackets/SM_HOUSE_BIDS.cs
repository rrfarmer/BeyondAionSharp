using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Taskmanager.Tasks.Housing;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_BIDS (Rolandas, Neon). Paged house auction bid list. Function&lt;HouseBids,Integer&gt;->Func&lt;HouseBids,int&gt;; HouseBids.Bid nested. HouseBids/HousingBidService/HousingService/AuctionEndTask red-tolerated.</summary>
public class SM_HOUSE_BIDS : AionServerPacket
{
    public const int STATIC_BODY_SIZE = 28;
    public static readonly Func<HouseBids, int> DYNAMIC_BODY_PART_SIZE_CALCULATOR = (bid) => 44;

    private readonly bool isFirst;
    private readonly bool isLast;
    private readonly List<HouseBids> houseBids;

    public SM_HOUSE_BIDS(bool isFirstPacket, bool isLastPacket, List<HouseBids> houseBids)
    {
        isFirst = isFirstPacket;
        isLast = isLastPacket;
        this.houseBids = houseBids;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        HouseBids.Bid lastBid = isLast ? HousingBidService.GetInstance().FindLastBid(player) : null;
        HouseBids bidsForRegisteredHouse = isLast ? HousingBidService.GetInstance().FindBidsForRegisteredHouse(player) : null;
        WriteC(isFirst ? 1 : 0);
        WriteC(isLast ? 1 : 0);

        WriteD(lastBid == null ? 0 : lastBid.GetListIndex());
        WriteQ(lastBid == null ? 0 : lastBid.GetKinah());

        WriteD(bidsForRegisteredHouse == null ? 0 : bidsForRegisteredHouse.GetListIndex());
        WriteQ(bidsForRegisteredHouse == null ? 0 : bidsForRegisteredHouse.GetInitialOffer().GetKinah()); // starting price

        WriteH(houseBids.Count);
        foreach (HouseBids bids in houseBids)
        {
            House house = HousingService.GetInstance().FindHouse(bids.GetHouseObjectId());
            WriteD(bids.GetListIndex());
            WriteD(house.GetLand().GetId());
            WriteD(house.GetAddress().GetId());
            WriteD(house.GetBuilding().GetId());
            WriteD(house.GetHouseType().GetId()); // client seems to ignore this
            WriteQ(bids.GetHighestBid().GetKinah());
            WriteQ(100000); // what's this? the same static value is sent in CM_REGISTER_HOUSE
            WriteD(bids.GetBidCount());
            WriteD(AuctionEndTask.GetInstance().GetRemainingAuctionSeconds(bids.GetHouseObjectId()));
        }
    }
}
