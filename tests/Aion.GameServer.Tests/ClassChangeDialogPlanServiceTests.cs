using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ClassChangeDialogPlanServiceTests
{
	// --- Dialog page ID lookup ---

	[Theory]
	[InlineData("ELYOS", "WARRIOR", 2375)]
	[InlineData("ASMODIANS", "WARRIOR", 3057)]
	[InlineData("ELYOS", "SCOUT", 2716)]
	[InlineData("ASMODIANS", "SCOUT", 3398)]
	[InlineData("ELYOS", "MAGE", 3057)]
	[InlineData("ASMODIANS", "MAGE", 3739)]
	[InlineData("ELYOS", "PRIEST", 3398)]
	[InlineData("ASMODIANS", "PRIEST", 4080)]
	[InlineData("ELYOS", "ENGINEER", 3739)]
	[InlineData("ASMODIANS", "ENGINEER", 3569)]
	[InlineData("ELYOS", "ARTIST", 4080)]
	[InlineData("ASMODIANS", "ARTIST", 3910)]
	public void CreateDialogPageIdPlan_ReturnsJavaPageId(string race, string playerClass, int expectedPageId)
	{
		var plan = ClassChangeDialogPlanService.CreateDialogPageIdPlan(race, playerClass);

		Assert.Equal(expectedPageId, plan.DialogPageId);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateDialogPageIdPlan_UnknownClassReturnsZero()
	{
		var plan = ClassChangeDialogPlanService.CreateDialogPageIdPlan("ELYOS", "GLADIATOR");

		Assert.Equal(0, plan.DialogPageId);
	}

	[Fact]
	public void CreateDialogPageIdPlan_IsCaseInsensitive()
	{
		var lower = ClassChangeDialogPlanService.CreateDialogPageIdPlan("elyos", "warrior");
		Assert.Equal(2375, lower.DialogPageId);
	}

	// --- Elyos class selection ---

	[Theory]
	[InlineData(2376, "GLADIATOR")]  // SELECT5_1
	[InlineData(2461, "TEMPLAR")]    // SELECT5_2
	[InlineData(2717, "ASSASSIN")]   // SELECT6_1
	[InlineData(2802, "RANGER")]     // SELECT6_2
	[InlineData(3058, "SORCERER")]   // SELECT7_1
	[InlineData(3143, "SPIRIT_MASTER")] // SELECT7_2
	[InlineData(3399, "CLERIC")]     // SELECT8_1
	[InlineData(3484, "CHANTER")]    // SELECT8_2
	[InlineData(3740, "GUNNER")]     // SELECT9_1
	[InlineData(3825, "RIDER")]      // SELECT9_2
	[InlineData(4081, "BARD")]       // SELECT10_1
	public void CreateClassSelectionPlan_ElyosDialogActionsMapToJavaClasses(int dialogAction, string expectedClass)
	{
		var plan = ClassChangeDialogPlanService.CreateClassSelectionPlan("ELYOS", dialogAction);

		Assert.True(plan.HasValidSelection);
		Assert.Equal(expectedClass, plan.SelectedClass);
		Assert.False(plan.IsLive);
	}

	// --- Asmodians class selection ---

	[Theory]
	[InlineData(3058, "GLADIATOR")]   // SELECT7_1
	[InlineData(3143, "TEMPLAR")]     // SELECT7_2
	[InlineData(3399, "ASSASSIN")]    // SELECT8_1
	[InlineData(3484, "RANGER")]      // SELECT8_2
	[InlineData(3740, "SORCERER")]    // SELECT9_1
	[InlineData(3825, "SPIRIT_MASTER")] // SELECT9_2
	[InlineData(4081, "CLERIC")]      // SELECT10_1
	[InlineData(4166, "CHANTER")]     // SELECT10_2
	[InlineData(3570, "GUNNER")]      // SELECT8_3_1
	[InlineData(3591, "RIDER")]       // SELECT8_3_2
	[InlineData(3911, "BARD")]        // SELECT9_3_1
	public void CreateClassSelectionPlan_AsmodiansDialogActionsMapToJavaClasses(int dialogAction, string expectedClass)
	{
		var plan = ClassChangeDialogPlanService.CreateClassSelectionPlan("ASMODIANS", dialogAction);

		Assert.True(plan.HasValidSelection);
		Assert.Equal(expectedClass, plan.SelectedClass);
	}

	[Fact]
	public void CreateClassSelectionPlan_UnknownActionReturnsNoSelection()
	{
		var plan = ClassChangeDialogPlanService.CreateClassSelectionPlan("ELYOS", dialogActionId: 9999);

		Assert.False(plan.HasValidSelection);
		Assert.Null(plan.SelectedClass);
	}

	[Fact]
	public void CreateClassSelectionPlan_UnknownRaceReturnsNoSelection()
	{
		var plan = ClassChangeDialogPlanService.CreateClassSelectionPlan("UNKNOWN", dialogActionId: 2376);

		Assert.False(plan.HasValidSelection);
		Assert.Null(plan.SelectedClass);
	}
}
