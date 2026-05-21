using System.Collections.Concurrent;
using Aion.GameServer.Configuration;

namespace Aion.GameServer.Services;

public sealed class HouseAuctionTimingService
{
	private static readonly TimeSpan ProlongationTime = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan MaxProlongationTime = TimeSpan.FromMinutes(30);
	private readonly JavaCronSchedule _auctionEndSchedule;
	private readonly TimeProvider _timeProvider;
	private readonly ConcurrentDictionary<int, DateTimeOffset> _prolongedAuctionEnds = new();

	public HouseAuctionTimingService(GameServerOptions? options = null, TimeProvider? timeProvider = null)
	{
		_auctionEndSchedule = JavaCronSchedule.WeeklyOrDefault(options?.Housing.AuctionEndTime, DayOfWeek.Sunday, 12);
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	public JavaCronSchedule AuctionEndSchedule => _auctionEndSchedule;

	public bool IsBiddingTime(int houseObjectId)
	{
		// Java parity: services/HousingBidService.isBiddingTime.
		var now = GetNow();
		return now.DayOfWeek != DayOfWeek.Sunday || now.Hour < 12 || IsAuctionProlonged(houseObjectId, now);
	}

	public bool TryProlongAuction(int houseObjectId)
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask.tryProlongAuction.
		var now = GetNow();
		var nextRegularEnd = GetNextRegularAuctionEnd(now);
		var millisUntilAuctionEnd = nextRegularEnd - now;
		var millisSinceLastAuctionEnd = now - nextRegularEnd.AddDays(-7);
		TimeSpan delay = TimeSpan.Zero;

		if (millisUntilAuctionEnd <= ProlongationTime)
			delay = millisUntilAuctionEnd + ProlongationTime;
		else if (millisSinceLastAuctionEnd >= TimeSpan.Zero && millisSinceLastAuctionEnd < MaxProlongationTime)
			delay = Min(MaxProlongationTime - millisSinceLastAuctionEnd, ProlongationTime);

		if (delay <= TimeSpan.Zero)
			return true;

		_prolongedAuctionEnds[houseObjectId] = now + delay;
		return true;
	}

	public int GetRemainingAuctionSeconds(int houseObjectId)
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask.getRemainingAuctionSeconds.
		var now = GetNow();
		var auctionEnd = TryGetActiveProlongedAuctionEnd(houseObjectId, now, out var prolongedEnd)
			? prolongedEnd
			: GetNextRegularAuctionEnd(now);
		return Math.Max(0, (int)(auctionEnd - now).TotalSeconds);
	}

	public void OnAuctionEnd(int houseObjectId)
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask.onAuctionEnd.
		_prolongedAuctionEnds.TryRemove(houseObjectId, out _);
	}

	public bool ShouldRunAuctionEndOnStartup(DateTimeOffset? serverStopTime, DateTimeOffset? startupTime = null)
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask.shouldRunOnStart.
		if (serverStopTime == null)
			return false;

		var lastPlannedRun = _auctionEndSchedule.GetPreviousRunBefore(startupTime ?? GetNow());
		if (serverStopTime.Value < lastPlannedRun)
			return true;

		return serverStopTime.Value - lastPlannedRun <= MaxProlongationTime;
	}

	private bool IsAuctionProlonged(int houseObjectId, DateTimeOffset now)
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask.isAuctionProlonged.
		return TryGetActiveProlongedAuctionEnd(houseObjectId, now, out _);
	}

	private bool TryGetActiveProlongedAuctionEnd(int houseObjectId, DateTimeOffset now, out DateTimeOffset auctionEnd)
	{
		if (_prolongedAuctionEnds.TryGetValue(houseObjectId, out auctionEnd))
		{
			if (auctionEnd > now)
				return true;
			_prolongedAuctionEnds.TryRemove(new KeyValuePair<int, DateTimeOffset>(houseObjectId, auctionEnd));
		}

		auctionEnd = default;
		return false;
	}

	private DateTimeOffset GetNextRegularAuctionEnd(DateTimeOffset now)
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask uses HousingConfig.HOUSE_AUCTION_END_TIME.
		return _auctionEndSchedule.GetNextRunAfter(now);
	}

	private DateTimeOffset GetNow()
	{
		return _timeProvider.GetLocalNow();
	}

	private static TimeSpan Min(TimeSpan left, TimeSpan right)
	{
		return left <= right ? left : right;
	}
}
