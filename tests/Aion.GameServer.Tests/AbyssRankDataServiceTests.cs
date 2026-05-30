using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AbyssRankDataServiceTests
{
	// --- Rank data table ---

	[Fact]
	public void AllRanks_Has18Entries()
	{
		Assert.Equal(18, AbyssRankDataService.AllRanks.Count);
	}

	[Fact]
	public void AllRanks_Grade9SoldierIsRankId1()
	{
		var rank = AbyssRankDataService.AllRanks[0];

		Assert.Equal(1, rank.Id);
		Assert.Equal(0, rank.Ordinal);
		Assert.Equal(300, rank.PointsGained);
		Assert.Equal(90, rank.PointsLost);
		Assert.Equal(0, rank.RequiredAp);
		Assert.Equal(0, rank.RequiredGp);
	}

	[Fact]
	public void AllRanks_SupremeCommanderIsRankId18()
	{
		var rank = AbyssRankDataService.AllRanks[17];

		Assert.Equal(18, rank.Id);
		Assert.Equal(17, rank.Ordinal);
		Assert.Equal(5916, rank.PointsGained);
		Assert.Equal(2366, rank.PointsLost);
		Assert.Equal(0, rank.RequiredAp);
		Assert.Equal(12437, rank.RequiredGp);
	}

	[Fact]
	public void GetById_ReturnsCorrectRank()
	{
		var rank = AbyssRankDataService.GetById(14); // STAR5_OFFICER

		Assert.NotNull(rank);
		Assert.Equal(14, rank!.Id);
		Assert.Equal(3705, rank.PointsGained);
		Assert.Equal(1482, rank.PointsLost);
	}

	[Fact]
	public void GetById_UnknownIdReturnsNull()
	{
		Assert.Null(AbyssRankDataService.GetById(99));
	}

	// --- getRankL10n ---

	[Fact]
	public void GetRankL10n_ElyosGrade9SoldierUsesBase901215()
	{
		// ordinal=0, base=901215 → L10n(901215 + 0) = L10n(901215)
		var l10n = AbyssRankDataService.GetRankL10n("ELYOS", 1);

		Assert.NotNull(l10n);
		Assert.StartsWith("$", l10n);
	}

	[Fact]
	public void GetRankL10n_AsmodiansUsesBase901233()
	{
		// Different base ID for Asmodians
		var elyos = AbyssRankDataService.GetRankL10n("ELYOS", 1);
		var asmo = AbyssRankDataService.GetRankL10n("ASMODIANS", 1);

		Assert.NotNull(elyos);
		Assert.NotNull(asmo);
		Assert.NotEqual(elyos, asmo); // Different bases → different L10n strings
	}

	[Fact]
	public void GetRankL10n_OrdinalAdvancesWithRank()
	{
		// Grade9=ordinal0, Grade8=ordinal1 → different L10n IDs
		var grade9 = AbyssRankDataService.GetRankL10n("ELYOS", 1);
		var grade8 = AbyssRankDataService.GetRankL10n("ELYOS", 2);

		Assert.NotEqual(grade9, grade8);
	}

	[Fact]
	public void AllRanks_IdsAreSequential1To18()
	{
		for (var i = 0; i < AbyssRankDataService.AllRanks.Count; i++)
			Assert.Equal(i + 1, AbyssRankDataService.AllRanks[i].Id);
	}
}
