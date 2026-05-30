using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DropRollNotificationPlanServiceTests
{
	[Fact]
	public void CreatePlan_ZeroRollProducesGiveupMessages()
	{
		var plan = DropRollNotificationPlanService.CreatePlan(requestedRoll: 0, maxRoll: 100, rollerName: "Alice");

		Assert.Equal(DropRollNotificationPlanStatus.Passed, plan.Status);
		Assert.Equal(0, plan.LuckResult);
		Assert.False(plan.IsLive);
		Assert.Equal(1390164, plan.SelfMessage.MessageId);   // DiceGiveupMe
		Assert.Equal(1390165, plan.OtherMessage.MessageId);  // DiceGiveupOther
		Assert.Contains("GIVEUP", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_NonZeroRollProducesResultMessages()
	{
		// Inject deterministic random for testing
		var plan = DropRollNotificationPlanService.CreatePlan(
			requestedRoll: 1, maxRoll: 100, rollerName: "Bob",
			randomInclusive: (min, max) => 42);

		Assert.Equal(DropRollNotificationPlanStatus.Rolled, plan.Status);
		Assert.Equal(42, plan.LuckResult);
		Assert.Equal(1390162, plan.SelfMessage.MessageId);   // DiceResultMe
		Assert.Equal(1390163, plan.OtherMessage.MessageId);  // DiceResultOther
		Assert.Contains("42", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_LuckIsWithinMaxRollRange()
	{
		var plan = DropRollNotificationPlanService.CreatePlan(
			requestedRoll: 5, maxRoll: 50, rollerName: "Carol",
			randomInclusive: (min, max) => { Assert.Equal(1, min); Assert.Equal(50, max); return 25; });

		Assert.Equal(25, plan.LuckResult);
	}

	[Fact]
	public void CreatePlan_MaxRollIsPreservedInPlan()
	{
		var plan = DropRollNotificationPlanService.CreatePlan(
			requestedRoll: 1, maxRoll: 200, rollerName: "Dave",
			randomInclusive: (_, _) => 1);

		Assert.Equal(200, plan.MaxRoll);
	}
}
