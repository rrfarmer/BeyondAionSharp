using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Extensions;
using Aion.GameServer.Utils.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SystemClockTests
{
    [Fact]
    public void RetailTickUsesTheSameOverrideAndUnsigned32WrapWithoutChangingWallTime()
    {
        long time = uint.MaxValue;
        SystemClock.UseSource(() => time);
        try
        {
            Assert.Equal(uint.MaxValue, SystemClock.RetailTick32());
            Assert.Equal(time, SystemClock.CurrentMillis());
            time += 2;
            Assert.Equal(1u, SystemClock.RetailTick32());
            Assert.Equal(4_294_967_297L, SystemClock.CurrentMillis());
        }
        finally { SystemClock.UseSystemClock(); }
    }

	[Fact]
	public async Task ScopedSourceWinsOverProcessSourceAndProcessSourceCrossesExecutionContexts()
	{
		const long processMillis = 1_700_000_000_123;
		const long scopedMillis = 1_800_000_000_987;
		SystemClock.SetProcessSource(() => processMillis);
		SystemClock.UseSource(() => scopedMillis);
		try
		{
			Assert.Equal(scopedMillis, SystemClock.CurrentMillis());
			Assert.Equal(scopedMillis / 1000, SystemClock.CurrentSeconds());
			Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(scopedMillis), SystemClock.UtcNow());

			Task<long> withoutExecutionContext;
			using (ExecutionContext.SuppressFlow())
				withoutExecutionContext = Task.Run(SystemClock.CurrentMillis);
			Assert.Equal(processMillis, await withoutExecutionContext);

			SystemClock.UseSystemClock();
			Assert.Equal(processMillis, SystemClock.CurrentMillis());
		}
		finally
		{
			SystemClock.UseSystemClock();
			SystemClock.UseSystemClockProcessWide();
		}
	}

	[Fact]
	public void CombatChainsAndCooldownsUseSystemClock()
	{
		long nowMillis = 1_700_000_000_000;
		SystemClock.UseSource(() => nowMillis);
		try
		{
			var chains = new ChainSkills();
			chains.UpdateChain("test", 1_000);
			Assert.Equal(1, chains.GetCurrentChainCount("test"));
			Assert.True(chains.CanContinue("test", 1, 3_000));

			nowMillis += 3_999;
			Assert.True(chains.CanContinue("test", 1, 3_000));
			nowMillis++;
			Assert.False(chains.CanContinue("test", 1, 3_000));

			var cooldowns = new Cooldowns();
			cooldowns.Put(7, nowMillis + 10_000);
			Assert.Equal(10, cooldowns.RemainingSeconds(7));

			nowMillis += 10_000;
			Assert.Null(cooldowns.Get(7));
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	[Fact]
	public void HousingAndExpirationHelpersUseSystemClock()
	{
		long nowMillis = 1_700_000_000_000;
		SystemClock.UseSource(() => nowMillis);
		try
		{
			var bids = new HouseBids(1, 10_000);
			Assert.Equal(nowMillis, bids.GetInitialOffer().GetTime());
			Assert.False(TimeUtil.IsExpired(nowMillis));

			nowMillis++;
			Assert.True(TimeUtil.IsExpired(nowMillis - 1));
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	[Fact]
	public async Task TimedItemExpiresAfterVirtualTimeAdvances()
	{
		const long epochMillis = 1_700_000_000_000;
		await using var pool = new VirtualThreadPool();
		SystemClock.UseSource(() => epochMillis + pool.NowMillis);
		try
		{
			var template = new ItemTemplate { expireTime = 1 };
			var item = new Item(1, template);

			Assert.False(item.IsExpired());
			Assert.False(((IExpirable)item).IsExpired());

			pool.Advance(TimeSpan.FromSeconds(61));

			Assert.True(item.IsExpired());
			Assert.True(((IExpirable)item).IsExpired());
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	[Fact]
	public void ServerTimeUsesSystemClockForInstantOffsetAndDaylightSavings()
	{
		TimeZoneInfo zone = CreateEasternTestZone();
		ServerTime.Initialize(zone);
		try
		{
			var summer = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
			SystemClock.UseSource(() => summer.ToUnixTimeMilliseconds());
			Assert.Equal(TimeSpan.FromHours(-4), ServerTime.Now().Offset);
			Assert.Equal(8, ServerTime.Now().Hour);
			Assert.Equal(-4 * 60 * 60, ServerTime.GetOffset());
			Assert.Equal(60 * 60, ServerTime.GetDaylightSavings());

			var winter = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
			SystemClock.UseSource(() => winter.ToUnixTimeMilliseconds());
			Assert.Equal(TimeSpan.FromHours(-5), ServerTime.Now().Offset);
			Assert.Equal(7, ServerTime.Now().Hour);
			Assert.Equal(-5 * 60 * 60, ServerTime.GetOffset());
			Assert.Equal(0, ServerTime.GetDaylightSavings());
		}
		finally
		{
			SystemClock.UseSystemClock();
			ServerTime.Initialize(TimeZoneInfo.Local);
		}
	}

	[Fact]
	public async Task ScheduledTaskDueTimeAndDelayUseSystemClock()
	{
		long nowMillis = 1_700_000_000_000;
		SystemClock.UseSource(() => nowMillis);
		try
		{
			await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
			ScheduledTask immediate = pool.Deferred(() => { });
			ScheduledTask future = pool.Deferred(() => { }, SystemClock.UtcNow().AddSeconds(10));

			Assert.Equal(0, immediate.GetDelay(TimeUnit.MILLISECONDS));
			Assert.Equal(10_000, future.GetDelay(TimeUnit.MILLISECONDS));
			nowMillis += 3_000;
			Assert.Equal(-3_000, immediate.GetDelay(TimeUnit.MILLISECONDS));
			Assert.Equal(7_000, future.GetDelay(TimeUnit.MILLISECONDS));

			immediate.Run();
			immediate.Get();
			future.Run();
			future.Get();
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	[Fact]
	public void QuestCompletionTimestampsUseSystemClock()
	{
		long nowMillis = 1_700_000_000_123;
		SystemClock.UseSource(() => nowMillis);
		try
		{
			var completedAtConstruction = new QuestState(1, QuestStatus.COMPLETE);
			Assert.Equal(SystemClock.UtcNow().UtcDateTime, completedAtConstruction.GetLastCompleteTime());

			var completedLater = new QuestState(2, QuestStatus.START);
			nowMillis += 42_000;
			completedLater.SetStatus(QuestStatus.COMPLETE);
			Assert.Equal(SystemClock.UtcNow().UtcDateTime, completedLater.GetLastCompleteTime());
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	private static TimeZoneInfo CreateEasternTestZone()
	{
		TimeZoneInfo.TransitionTime starts = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
			new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
		TimeZoneInfo.TransitionTime ends = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
			new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
		TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
			new DateTime(2020, 1, 1),
			new DateTime(2030, 12, 31),
			TimeSpan.FromHours(1),
			starts,
			ends);
		return TimeZoneInfo.CreateCustomTimeZone(
			"P4-02-Eastern",
			TimeSpan.FromHours(-5),
			"P4-02 Eastern",
			"P4-02 Eastern Standard",
			"P4-02 Eastern Daylight",
			[rule]);
	}
}
