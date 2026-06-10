using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.House;

/// <summary>Java parity: model/house/HouseBids (Neon). AtomicInteger→Interlocked.Increment; non-static inner Bid→nested class holding outer reference; synchronized→lock(this); currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; removeAll→RemoveAll(Contains). HousingConfig red-tolerated.</summary>
public class HouseBids
{
    private static int counter;
    private readonly int listIndex;
    private readonly int houseObjectId;
    private readonly long registrationFee;
    private readonly List<Bid> bids = new();

    public HouseBids(int houseObjectId, long initialPrice)
        : this(houseObjectId, initialPrice, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    {
    }

    public HouseBids(int houseObjectId, long initialPrice, long time)
    {
        this.listIndex = Interlocked.Increment(ref counter);
        this.houseObjectId = houseObjectId;
        this.registrationFee = (long)(initialPrice * HousingConfig.AUCTION_REGISTRATION_FEE_PERCENT);
        bids.Add(new Bid(this, 0, initialPrice, time));
    }

    public int GetListIndex()
    {
        return listIndex;
    }

    public int GetHouseObjectId()
    {
        return houseObjectId;
    }

    /// <returns>Players bid if bidding was successful, meaning he is the highest bidder.</returns>
    public Bid DoBid(Player player, long bidKinah)
    {
        return DoBid(player.GetObjectId(), bidKinah, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    /// <returns>Players bid if bidding was successful, meaning he is the highest bidder.</returns>
    public Bid DoBid(int playerObjectId, long bidKinah, long time)
    {
        lock (this)
        {
            Bid highestBid = GetHighestBid();
            if (highestBid.GetKinah() < bidKinah || highestBid == GetInitialOffer() && highestBid.GetKinah() == bidKinah)
            {
                Bid bid = new(this, playerObjectId, bidKinah, time);
                bids.Add(bid);
                return bid;
            }
            return null;
        }
    }

    public bool IsHighestBidder(Player player)
    {
        return GetHighestBid().GetPlayerObjectId() == player.GetObjectId();
    }

    public Bid GetHighestBid()
    {
        lock (this)
        {
            return bids[bids.Count - 1];
        }
    }

    public Bid GetLatestBid(Player player)
    {
        lock (this)
        {
            for (int i = bids.Count - 1; i >= 0; i--)
            {
                Bid bid = bids[i];
                if (bid.GetPlayerObjectId() == player.GetObjectId())
                    return bid;
            }
            return null;
        }
    }

    public Bid GetInitialOffer()
    {
        lock (this)
        {
            return bids[0];
        }
    }

    public int GetBidCount()
    {
        lock (this)
        {
            return bids.Count - 1; // first bid is initialPrice
        }
    }

    /// <summary>
    /// Deletes all bids of given player which are not the highest bid. Disables the bid by setting bidder ID to 0 if it's the highest bid, because
    /// otherwise another bidder may become the highest bidder for two houses. Auction winner with bidder ID 0 will be handled gracefully on auction end.
    /// </summary>
    public List<Bid> DeleteOrDisableBids(int playerObjectId)
    {
        lock (this)
        {
            List<Bid> bidsToDelete = new();
            for (int i = 1, indexOfHighestBid = bids.Count - 1; i <= indexOfHighestBid; i++)
            {
                Bid bid = bids[i];
                if (bid.GetPlayerObjectId() == playerObjectId)
                {
                    if (i == 1 || i < indexOfHighestBid)
                        bidsToDelete.Add(bid);
                    else
                        bid.playerObjectId = 0;
                }
            }
            bids.RemoveAll(b => bidsToDelete.Contains(b));
            return bidsToDelete;
        }
    }

    public class Bid
    {
        private readonly HouseBids outer;
        internal int playerObjectId;
        private readonly long kinah;
        private readonly long time;

        internal Bid(HouseBids outer, int playerObjectId, long kinah, long time)
        {
            this.outer = outer;
            this.playerObjectId = playerObjectId;
            this.kinah = kinah;
            this.time = time;
        }

        public int GetListIndex()
        {
            return outer.listIndex;
        }

        public int GetHouseObjectId()
        {
            return outer.houseObjectId;
        }

        /// <returns>The player object ID of this bid. 0 if it's the initial offer or the player got deleted.</returns>
        public int GetPlayerObjectId()
        {
            return playerObjectId;
        }

        public long GetKinah()
        {
            return kinah;
        }

        public long GetTime()
        {
            return time;
        }

        public long CalculateSalesCommission()
        {
            return (long)(kinah * HousingConfig.AUCTION_SALES_COMMISION_PERCENT);
        }

        public long CalculateSaleRewardKinah()
        {
            return kinah - CalculateSalesCommission() + outer.registrationFee;
        }
    }
}
