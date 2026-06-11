using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Taskmanager.Tasks.Housing;

/// <summary>
/// Handles housing auction end and potential prolongations if there are new bids just before auction end (Java parity:
/// taskmanager/tasks/housing/AuctionEndTask, Neon) : AbstractCronTask. TimeUnit.MINUTES.toMillis→*60*1000; ConcurrentHashMap→
/// ConcurrentDictionary; **Map.compute (null=remove) → lock + TryGetValue/indexer/TryRemove** (no atomic C# equivalent);
/// Future→IScheduledFuture; anonymous Runnable→Schedule(ct=>...). HousingBidService red-tolerated.
/// </summary>
public class AuctionEndTask : AbstractCronTask
{
    private static readonly long PROLONGATION_MILLIS = 5L * 60 * 1000;
    private static readonly long MAX_PROLONGATION_MILLIS = 30L * 60 * 1000;
    private static readonly AuctionEndTask instance = new();
    private ConcurrentDictionary<int, ProlongedAuction> prolongedAuctions = new();

    public static AuctionEndTask GetInstance()
    {
        return instance;
    }

    private AuctionEndTask() : base(HousingConfig.HOUSE_AUCTION_END_TIME)
    {
    }

    protected override bool ShouldRunOnStart()
    {
        if (base.ShouldRunOnStart()) // true if the server was down when auctions should have ended (SERVER_STOP_MILLIS < lastPlannedRun)
            return true;
        if (SERVER_STOP_MILLIS != null) // trigger auction end if the server shut down in the prolongation time frame (30min after regular auction end)
            return SERVER_STOP_MILLIS - GetLastPlannedRun().ToUnixTimeMilliseconds() <= MAX_PROLONGATION_MILLIS;
        return false;
    }

    protected override void ExecuteTask()
    {
        HousingBidService.GetInstance().EndAuctions();
    }

    public int GetRemainingAuctionSeconds(int houseObjectId)
    {
        ProlongedAuction prolongedAuction = prolongedAuctions.GetValueOrDefault(houseObjectId);
        long auctionEndMillis = prolongedAuction == null ? GetNextRun().ToUnixTimeMilliseconds() : prolongedAuction.auctionEndMillis;
        return (int)((auctionEndMillis - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
    }

    public void OnAuctionEnd(int houseObjectId)
    {
        if (prolongedAuctions.TryRemove(houseObjectId, out ProlongedAuction prolongedAuction))
            prolongedAuction.task.Cancel(false);
    }

    /// <returns>True if the auction did not need to be prolonged or was prolonged successfully. False if the auction just ended.</returns>
    public bool TryProlongAuction(int houseObjectId)
    {
        long millisUntilAuctionEnd = GetMillisUntilNextRun();
        long millisSinceLastAuctionEnd = GetMillisSinceLastRun();
        long delayMillis = 0;
        if (millisUntilAuctionEnd <= 5L * 60 * 1000) // initial extension is 5 minutes after regular auction end
            delayMillis = millisUntilAuctionEnd + PROLONGATION_MILLIS;
        else if (millisSinceLastAuctionEnd != -1 && millisSinceLastAuctionEnd < MAX_PROLONGATION_MILLIS) // max extension is 30 minutes
            delayMillis = Math.Min(30L * 60 * 1000 - millisSinceLastAuctionEnd, PROLONGATION_MILLIS);
        return delayMillis == 0 || ProlongAuction(houseObjectId, delayMillis);
    }

    public bool IsAuctionProlonged(int houseObjectId)
    {
        return prolongedAuctions.ContainsKey(houseObjectId);
    }

    /// <returns>True if the auction could be prolonged. False if it just ended.</returns>
    private bool ProlongAuction(int houseObjectId, long delayMillis)
    {
        // Java Map.compute(key, remappingFn) atomic: null return removes the entry. No ConcurrentDictionary equivalent → lock.
        ProlongedAuction prolongedAuction;
        lock (prolongedAuctions)
        {
            prolongedAuctions.TryGetValue(houseObjectId, out ProlongedAuction oldValue);
            if (oldValue == null)
            {
                oldValue = new ProlongedAuction(houseObjectId, delayMillis);
                prolongedAuctions[houseObjectId] = oldValue;
                prolongedAuction = oldValue;
            }
            else if (!oldValue.Prolong(delayMillis))
            {
                prolongedAuctions.TryRemove(houseObjectId, out _);
                prolongedAuction = null;
            }
            else
            {
                prolongedAuction = oldValue;
            }
        }
        return prolongedAuction != null;
    }

    private class ProlongedAuction
    {
        private readonly int houseObjectId;
        internal long auctionEndMillis;
        internal ScheduledTask task;

        internal ProlongedAuction(int houseObjectId, long delayMillis)
        {
            this.houseObjectId = houseObjectId;
            Prolong(delayMillis);
        }

        internal bool Prolong(long delayMillis)
        {
            if (task != null && !task.Cancel(false))
                return false;
            auctionEndMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMillis;
            task = ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                HousingBidService.GetInstance().EndAuction(houseObjectId);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(delayMillis));
            return true;
        }
    }
}
