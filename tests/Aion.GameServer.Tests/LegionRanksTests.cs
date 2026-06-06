using Aion.GameServer.Model.Legion;

namespace Aion.GameServer.Tests;

public sealed class LegionRanksTests
{
	[Theory]
	// Java parity: LegionRank ordinals (client rank ids).
	[InlineData("BRIGADE_GENERAL", 0)]
	[InlineData("DEPUTY", 1)]
	[InlineData("CENTURION", 2)]
	[InlineData("LEGIONARY", 3)]
	[InlineData("VOLUNTEER", 4)]
	public void GetRankId_MapsEnumNameToJavaOrdinal(string rank, int expected)
	{
		Assert.Equal(expected, LegionRanks.GetRankId(rank));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("NOT_A_RANK")]
	public void GetRankId_UnknownOrBlank_ReturnsMinusOne(string? rank)
	{
		Assert.Equal(-1, LegionRanks.GetRankId(rank));
	}

	[Theory]
	// Java parity: LegionService.appointRank uses LegionRank.values()[rankId].
	[InlineData(0, "BRIGADE_GENERAL")]
	[InlineData(1, "DEPUTY")]
	[InlineData(2, "CENTURION")]
	[InlineData(3, "LEGIONARY")]
	[InlineData(4, "VOLUNTEER")]
	public void FromRankId_MapsJavaOrdinalToEnumName(int rankId, string expected)
	{
		Assert.Equal(expected, LegionRanks.FromRankId(rankId));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(5)]
	public void FromRankId_InvalidRankIdReturnsNull(int rankId)
	{
		Assert.Null(LegionRanks.FromRankId(rankId));
	}

	[Fact]
	public void IsBrigadeGeneral_OnlyTrueForBrigadeGeneral()
	{
		// Java parity: LegionMember.isBrigadeGeneral() -> rank == LegionRank.BRIGADE_GENERAL.
		Assert.True(LegionRanks.IsBrigadeGeneral("BRIGADE_GENERAL"));
		Assert.False(LegionRanks.IsBrigadeGeneral("DEPUTY"));
		Assert.False(LegionRanks.IsBrigadeGeneral("VOLUNTEER"));
		Assert.False(LegionRanks.IsBrigadeGeneral(""));
		Assert.False(LegionRanks.IsBrigadeGeneral(null));
	}

	[Fact]
	public void Default_IsVolunteer()
	{
		// Java parity: LegionMember default rank = LegionRank.VOLUNTEER.
		Assert.Equal("VOLUNTEER", LegionRanks.Default);
	}

	[Fact]
	public void PlayerIsBrigadeGeneral_ReflectsLegionRank()
	{
		// Java parity: Player.getLegionMember().isBrigadeGeneral() bridged via Player.IsBrigadeGeneral.
		var bg = new Model.GameObjects.Player { LegionRank = "BRIGADE_GENERAL" };
		var deputy = new Model.GameObjects.Player { LegionRank = "DEPUTY" };
		var noLegion = new Model.GameObjects.Player(); // LegionRank defaults to empty
		Assert.True(bg.IsBrigadeGeneral);
		Assert.False(deputy.IsBrigadeGeneral);
		Assert.False(noLegion.IsBrigadeGeneral);
	}
}
