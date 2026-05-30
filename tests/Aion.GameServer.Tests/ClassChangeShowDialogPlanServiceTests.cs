using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ClassChangeShowDialogPlanServiceTests
{
	[Theory]
	[InlineData("ELYOS", "WARRIOR", 2375, 1006)]
	[InlineData("ASMODIANS", "WARRIOR", 3057, 2008)]
	[InlineData("ELYOS", "SCOUT", 2716, 1006)]
	[InlineData("ASMODIANS", "MAGE", 3739, 2008)]
	public void CreatePlan_Level9StartingClassProducesDialogWindow(string race, string playerClass, int expectedPageId, int expectedQuestId)
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(playerLevel: 9, race, playerClass);

		Assert.Equal(ClassChangeShowDialogPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendDialogWindow);
		Assert.NotNull(plan.DialogWindowPacket);
		Assert.Equal(expectedPageId, plan.DialogPageId);
		Assert.Equal(expectedQuestId, plan.AscensionQuestId);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(8)]
	public void CreatePlan_LevelBelowNineSkips(int level)
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(level, "ELYOS", "WARRIOR");

		Assert.Equal(ClassChangeShowDialogPlanStatus.SkippedLevelTooLow, plan.Status);
		Assert.False(plan.ShouldSendDialogWindow);
		Assert.Null(plan.DialogWindowPacket);
	}

	[Theory]
	[InlineData("GLADIATOR")]
	[InlineData("TEMPLAR")]
	[InlineData("ASSASSIN")]
	[InlineData("SORCERER")]
	[InlineData("CLERIC")]
	public void CreatePlan_NonStartingClassSkips(string nonStartingClass)
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(playerLevel: 10, "ELYOS", nonStartingClass);

		Assert.Equal(ClassChangeShowDialogPlanStatus.SkippedNotStartingClass, plan.Status);
		Assert.False(plan.ShouldSendDialogWindow);
		Assert.Null(plan.DialogWindowPacket);
	}

	[Fact]
	public void CreatePlan_ElyosQuestIdIs1006()
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(9, "ELYOS", "WARRIOR");

		Assert.Equal(1006, plan.AscensionQuestId);
	}

	[Fact]
	public void CreatePlan_AsmodiansQuestIdIs2008()
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(9, "ASMODIANS", "WARRIOR");

		Assert.Equal(2008, plan.AscensionQuestId);
	}

	[Fact]
	public void CreatePlan_HighLevelStartingClassStillWorks()
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(playerLevel: 65, "ELYOS", "PRIEST");

		Assert.Equal(ClassChangeShowDialogPlanStatus.PlanCreated, plan.Status);
		Assert.IsType<SmDialogWindow>(plan.DialogWindowPacket);
	}

	[Fact]
	public void CreatePlan_IsCaseInsensitive()
	{
		var plan = ClassChangeShowDialogPlanService.CreatePlan(9, "elyos", "warrior");

		Assert.Equal(ClassChangeShowDialogPlanStatus.PlanCreated, plan.Status);
		Assert.Equal(2375, plan.DialogPageId);
	}
}
