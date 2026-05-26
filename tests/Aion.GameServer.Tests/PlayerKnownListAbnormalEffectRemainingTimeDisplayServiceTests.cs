using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListAbnormalEffectRemainingTimeDisplayServiceTests
{
	[Theory]
	[InlineData(null)]
	[InlineData(0)]
	public void Resolve_NullOrZeroDurationMatchesJavaPermanentDisplaySentinel(int? durationMillis)
	{
		var remaining = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				durationMillis,
				EndTimeUnixTimeMilliseconds: 1_100,
				EffectedIsNpc: false,
				NowUnixTimeMilliseconds: 1_000));

		Assert.Equal(-1, remaining);
	}

	[Theory]
	[InlineData(86_399_999, 86_398_999)]
	[InlineData(86_400_000, -1)]
	public void Resolve_NpcDurationAtLeastTwentyFourHoursMatchesJavaSentinel(
		int durationMillis,
		int expected)
	{
		var remaining = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				durationMillis,
				EndTimeUnixTimeMilliseconds: 86_400_000,
				EffectedIsNpc: true,
				NowUnixTimeMilliseconds: 1_001));

		Assert.Equal(expected, remaining);
	}

	[Fact]
	public void Resolve_PlayerTwentyFourHourDurationUsesRemainingTime()
	{
		var remaining = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				86_400_000,
				EndTimeUnixTimeMilliseconds: 86_400_000,
				EffectedIsNpc: false,
				NowUnixTimeMilliseconds: 1_000));

		Assert.Equal(86_399_000, remaining);
	}

	[Fact]
	public void Resolve_RemainingTimeGreaterThanIntegerMaxMatchesJavaOverflowSentinel()
	{
		var remaining = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				int.MaxValue,
				EndTimeUnixTimeMilliseconds: (long)int.MaxValue + 2,
				EffectedIsNpc: false,
				NowUnixTimeMilliseconds: 0));

		Assert.Equal(-1, remaining);
	}

	[Fact]
	public void Resolve_OrdinaryRemainingTimeAndExpiredNegativeValuesUseJavaIntCast()
	{
		var ordinary = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				30_000,
				EndTimeUnixTimeMilliseconds: 45_000,
				EffectedIsNpc: false,
				NowUnixTimeMilliseconds: 15_000));
		var expired = PlayerKnownListAbnormalEffectRemainingTimeDisplayService.Resolve(
			new PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
				30_000,
				EndTimeUnixTimeMilliseconds: 1_000,
				EffectedIsNpc: false,
				NowUnixTimeMilliseconds: 2_500));

		Assert.Equal(30_000, ordinary);
		Assert.Equal(-1_500, expired);
	}
}
