using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class GameUtilFormulaServiceTests
{
	// --- ConvertName ---

	[Fact]
	public void ConvertName_NormalizesToTitleCase()
	{
		// "atracer" → "Atracer"
		Assert.Equal("Atracer", GameUtilFormulaService.ConvertName("atracer", allowCustomNames: false));
	}

	[Fact]
	public void ConvertName_AllCapsToTitleCase()
	{
		// "DAEVA" → "Daeva"
		Assert.Equal("Daeva", GameUtilFormulaService.ConvertName("DAEVA", allowCustomNames: false));
	}

	[Fact]
	public void ConvertName_AllowCustomNamesReturnsAsIs()
	{
		Assert.Equal("myDaeva123", GameUtilFormulaService.ConvertName("myDaeva123", allowCustomNames: true));
	}

	[Fact]
	public void ConvertName_EmptyStringReturnsEmpty()
	{
		Assert.Equal(string.Empty, GameUtilFormulaService.ConvertName(string.Empty, allowCustomNames: false));
	}

	// --- IsExpired ---

	[Fact]
	public void IsExpired_PastTimeIsExpired()
	{
		Assert.True(GameUtilFormulaService.IsExpired(timeMillis: 1000, currentTimeMillis: 2000));
	}

	[Fact]
	public void IsExpired_FutureTimeIsNotExpired()
	{
		Assert.False(GameUtilFormulaService.IsExpired(timeMillis: 3000, currentTimeMillis: 2000));
	}

	[Fact]
	public void IsExpired_ExactCurrentTimeIsNotExpired()
	{
		// Java: time < currentTime (strict less-than)
		Assert.False(GameUtilFormulaService.IsExpired(timeMillis: 2000, currentTimeMillis: 2000));
	}

	// --- GetMembershipRate ---

	[Fact]
	public void GetMembershipRate_SelectsByMembershipIndex()
	{
		var rates = new float[] { 1.0f, 2.0f, 3.0f };

		Assert.Equal(1.0f, GameUtilFormulaService.GetMembershipRate(0, rates));
		Assert.Equal(2.0f, GameUtilFormulaService.GetMembershipRate(1, rates));
		Assert.Equal(3.0f, GameUtilFormulaService.GetMembershipRate(2, rates));
	}

	[Fact]
	public void GetMembershipRate_ClampsHighMembershipToLastIndex()
	{
		// Java: min(rates.length - 1, membershipLevel)
		var rates = new float[] { 1.0f, 2.0f };
		Assert.Equal(2.0f, GameUtilFormulaService.GetMembershipRate(99, rates));
	}

	[Fact]
	public void GetMembershipRate_EmptyArrayReturnsOne()
	{
		Assert.Equal(1f, GameUtilFormulaService.GetMembershipRate(0, []));
	}

	// --- CanBeMentoredBy ---

	[Fact]
	public void CanBeMentoredBy_TenLevelDifferenceAllows()
	{
		// player 30, mentor 40 → 30+10 = 40 <= 40 → true
		Assert.True(GameUtilFormulaService.CanBeMentoredBy(30, 40));
	}

	[Fact]
	public void CanBeMentoredBy_ElevenLevelDifferenceAllows()
	{
		Assert.True(GameUtilFormulaService.CanBeMentoredBy(29, 40));
	}

	[Fact]
	public void CanBeMentoredBy_NineLevelDifferenceDisallows()
	{
		// 31+10 = 41 > 40 → false
		Assert.False(GameUtilFormulaService.CanBeMentoredBy(31, 40));
	}
}
