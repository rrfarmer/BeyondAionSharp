using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetCommonDataTimingTests
{
	[Fact]
	public void ToBirthdayEpochSecondsReturnsZeroForMissingTimestampLikeJava()
	{
		Assert.Equal(0, PetCommonDataTiming.ToBirthdayEpochSeconds(null));
	}

	[Fact]
	public void ToBirthdayEpochSecondsConvertsTimestampMillisLikeJava()
	{
		var birthday = DateTimeOffset.FromUnixTimeMilliseconds(1_234_567);

		Assert.Equal(1234, PetCommonDataTiming.ToBirthdayEpochSeconds(birthday));
	}

	[Fact]
	public void GetRefeedDelayReturnsRemainingMillisAndClearsExpiredTimeLikeJava()
	{
		var timing = new PetCommonDataTiming();
		timing.SetRefeedTime(10_000);

		Assert.Equal(2_500, timing.GetRefeedDelay(currentTimeMillis: 7_500));
		Assert.Equal(10_000, timing.RefeedTimeMillis);

		Assert.Equal(0, timing.GetRefeedDelay(currentTimeMillis: 10_001));
		Assert.Equal(0, timing.RefeedTimeMillis);
	}

	[Fact]
	public void GetMoodPointsLazilyStartsClockAndCapsPacketValueLikeJava()
	{
		var timing = new PetCommonDataTiming();

		Assert.Equal(0, timing.GetMoodPoints(forPacket: true, currentTimeMillis: 20_000));
		Assert.Equal(20_000, timing.StartMoodTimeMillis);

		timing.SetShuggleCounter(10);

		Assert.Equal(9000, timing.GetMoodPoints(forPacket: true, currentTimeMillis: 20_499));
		Assert.Equal(10_000, timing.GetMoodPoints(forPacket: false, currentTimeMillis: 20_499));
	}

	[Fact]
	public void GetMoodPointsUsesJavaRoundFloatSeconds()
	{
		var timing = new PetCommonDataTiming();
		timing.SetStartMoodTime(10_000);

		Assert.Equal(0, timing.GetMoodPoints(forPacket: false, currentTimeMillis: 10_499));
		Assert.Equal(1, timing.GetMoodPoints(forPacket: false, currentTimeMillis: 10_500));
		Assert.Equal(1, timing.GetMoodPoints(forPacket: false, currentTimeMillis: 11_499));
		Assert.Equal(2, timing.GetMoodPoints(forPacket: false, currentTimeMillis: 11_500));
	}

	[Fact]
	public void GetMoodRemainingTimeReturnsSecondsAndClearsExpiredCooldownLikeJava()
	{
		var timing = new PetCommonDataTiming();
		timing.SetMoodCooldownStarted(10_000);

		Assert.Equal(599, timing.GetMoodRemainingTime(currentTimeMillis: 10_001));
		Assert.Equal(10_000, timing.MoodCooldownStartedMillis);

		Assert.Equal(0, timing.GetMoodRemainingTime(currentTimeMillis: 610_000));
		Assert.Equal(0, timing.MoodCooldownStartedMillis);
	}

	[Fact]
	public void GetGiftRemainingTimeReturnsSecondsAndClearsExpiredCooldownLikeJava()
	{
		var timing = new PetCommonDataTiming();
		timing.SetGiftCooldownStarted(10_000);

		Assert.Equal(3599, timing.GetGiftRemainingTime(currentTimeMillis: 10_001));
		Assert.Equal(10_000, timing.GiftCooldownStartedMillis);

		Assert.Equal(0, timing.GetGiftRemainingTime(currentTimeMillis: 3_610_000));
		Assert.Equal(0, timing.GiftCooldownStartedMillis);
	}

	[Fact]
	public void IncreaseShuggleCounterHonorsMoodCooldownAndSetsCooldownStartLikeJava()
	{
		var timing = new PetCommonDataTiming();
		timing.SetShuggleCounter(2);
		timing.SetMoodCooldownStarted(10_000);

		Assert.False(timing.IncreaseShuggleCounter(currentTimeMillis: 20_000));
		Assert.Equal(2, timing.ShuggleCounter);
		Assert.Equal(10_000, timing.MoodCooldownStartedMillis);

		Assert.True(timing.IncreaseShuggleCounter(currentTimeMillis: 610_000));
		Assert.Equal(3, timing.ShuggleCounter);
		Assert.Equal(610_000, timing.MoodCooldownStartedMillis);
	}

	[Fact]
	public void ClearMoodStatisticsResetsStartAndCounterOnlyLikeJava()
	{
		var timing = new PetCommonDataTiming();
		timing.SetStartMoodTime(10_000);
		timing.SetShuggleCounter(4);
		timing.SetMoodCooldownStarted(11_000);
		timing.SetGiftCooldownStarted(12_000);
		timing.SetLastSentPoints(500);

		timing.ClearMoodStatistics();

		Assert.Equal(0, timing.StartMoodTimeMillis);
		Assert.Equal(0, timing.ShuggleCounter);
		Assert.Equal(11_000, timing.MoodCooldownStartedMillis);
		Assert.Equal(12_000, timing.GiftCooldownStartedMillis);
		Assert.Equal(500, timing.LastSentPoints);
	}
}
