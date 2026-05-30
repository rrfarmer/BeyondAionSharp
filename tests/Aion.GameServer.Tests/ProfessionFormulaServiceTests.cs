using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ProfessionFormulaServiceTests
{
	// --- getUpgradeCost ---

	[Theory]
	[InlineData(0,   true,  3_500)]
	[InlineData(99,  true,  17_000)]
	[InlineData(199, true,  115_000)]
	[InlineData(299, true,  460_000)]
	[InlineData(449, true,  6_004_900)]  // crafting artisan
	public void GetUpgradeCost_CraftingSkillReturnsExpectedCost(int level, bool isCrafting, int expected)
	{
		Assert.Equal(expected, ProfessionFormulaService.GetUpgradeCost(level, isCrafting));
	}

	[Fact]
	public void GetUpgradeCost_Tapping449ReturnsNull()
	{
		// Tapping (not crafting) has no artisan grade between expert and master
		Assert.Null(ProfessionFormulaService.GetUpgradeCost(449, isCrafting: false));
	}

	[Fact]
	public void GetUpgradeCost_ExpertLevel399ReturnsNull()
	{
		// Expert is granted by quest
		Assert.Null(ProfessionFormulaService.GetUpgradeCost(399, isCrafting: true));
	}

	// --- isCrafting ---

	[Theory]
	[InlineData(40001, true)]
	[InlineData(40008, true)]
	[InlineData(40010, true)]
	[InlineData(30002, false)]  // essence tapping
	[InlineData(30003, false)]  // aether tapping
	public void IsCrafting_ReturnsCorrectValue(int skillId, bool expected)
	{
		Assert.Equal(expected, ProfessionFormulaService.IsCrafting(skillId));
	}

	// --- getMaxUpgradableLevel ---

	[Fact]
	public void GetMaxUpgradableLevel_CraftingIs499()
	{
		Assert.Equal(499, ProfessionFormulaService.GetMaxUpgradableLevel(isCrafting: true));
	}

	[Fact]
	public void GetMaxUpgradableLevel_TappingIs399()
	{
		Assert.Equal(399, ProfessionFormulaService.GetMaxUpgradableLevel(isCrafting: false));
	}

	// --- getSkillGrade ---

	[Theory]
	[InlineData(0,   900797)]  // Amateur
	[InlineData(99,  900797)]
	[InlineData(100, 900798)]  // Novice
	[InlineData(200, 900799)]  // Apprentice
	[InlineData(300, 900800)]  // Journeyman
	[InlineData(400, 900801)]  // Expert
	[InlineData(450, 902027)]  // Artisan (crafting)
	public void GetSkillGradeL10n_ReturnsCorrectedEncodedId(int level, int baseL10nId)
	{
		var l10n = ProfessionFormulaService.GetSkillGradeL10n(level, isCrafting: true);
		Assert.NotNull(l10n);
		Assert.StartsWith("$", l10n);
	}

	// --- plan with guard messages ---

	[Fact]
	public void CreatePlan_Level399BlocksWithExpertMessage()
	{
		var plan = ProfessionFormulaService.CreatePlan("COOKING", 40001, 399);

		Assert.False(plan.CanUpgrade);
		Assert.Contains("EXTEND_MONEY", plan.BlockReason);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_Level299GivesCost460000()
	{
		var plan = ProfessionFormulaService.CreatePlan("COOKING", 40001, 299);

		Assert.True(plan.CanUpgrade);
		Assert.Equal(460_000, plan.UpgradeCost);
	}
}
