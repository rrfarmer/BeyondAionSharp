using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillBuffStatFunctionPlanServiceTests
{
	[Fact]
	public void CreateRegistryPlan_ReportsNoFunctionsWhenBuffEffectHasNoChanges()
	{
		var plan = SkillBuffStatFunctionPlanService.CreateRegistryPlan(8472, "boostdroprate", 1, []);

		Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.NoStatFunctions, plan.Status);
		Assert.False(plan.IsReadyForRegistry);
		Assert.Empty(plan.Functions);
		Assert.Contains("live Effect StatOwner provider", plan.MissingInputs);
		Assert.Contains("live CreatureGameStats stat-function registry", plan.MissingInputs);
		Assert.Contains("BufEffect.getModifiers", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateRegistryPlan_MapsBufEffectChangesToJavaFunctionTypesAndRegistryOrder()
	{
		var plan = SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			9878,
			"drboost",
			2,
			[
				new SkillStatChange("DR_BOOST", "ADD", 7, 5),
				new SkillStatChange("DR_BOOST", "PERCENT", 50, 0),
				new SkillStatChange("DR_BOOST", "REPLACE", 80, 10)
			]);

		Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingEffectStatOwnerProvider, plan.Status);
		Assert.Equal(["StatSetFunction", "StatRateFunction", "StatAddFunction"], plan.Functions.Select(function => function.JavaFunctionType));
		Assert.Equal([100, 50, 17], plan.Functions.Select(function => function.EffectiveValue));
		Assert.Equal([40, 50, 60], plan.Functions.Select(function => function.Priority));
		Assert.Equal([false, true, true], plan.Functions.Select(function => function.IsBonus));
		Assert.All(plan.Functions, function => Assert.True(function.RequiresStatFunctionProxy));
		Assert.All(plan.Functions, function => Assert.True(function.IsSupported));
	}

	[Fact]
	public void CreateRegistryPlan_PreservesSourceOrderWithinSamePriority()
	{
		var plan = SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			8472,
			"boostdroprate",
			1,
			[
				new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0),
				new SkillStatChange("DR_BOOST", "ADD", 30, 0)
			]);

		Assert.Equal([0, 1], plan.Functions.Select(function => function.SourceIndex));
		Assert.Equal(["BOOST_DROP_RATE", "DR_BOOST"], plan.Functions.Select(function => function.StatName));
	}

	[Fact]
	public void CreateRegistryPlan_BlocksConditionedFunctionsUntilValidatorProviderExists()
	{
		var change = new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0);
		change.AddCondition(new SkillStatChangeConditionSummary("weapon", new Dictionary<string, string>(StringComparer.Ordinal) { ["weapon"] = "ORB" }));

		var plan = SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			8472,
			"boostdroprate",
			1,
			[change],
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true);

		Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.BlockedMissingConditionValidatorProvider, plan.Status);
		Assert.False(plan.IsReadyForRegistry);
		var function = Assert.Single(plan.Functions);
		Assert.True(function.HasConditions);
		Assert.Contains("live Conditions.validate provider", plan.MissingInputs);
		Assert.DoesNotContain("live Effect StatOwner provider", plan.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats stat-function registry", plan.MissingInputs);
	}

	[Fact]
	public void CreateRegistryPlan_ReportsUnsupportedFunctionBeforeLiveProviderReadiness()
	{
		var plan = SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			8472,
			"boostdroprate",
			1,
			[new SkillStatChange("BOOST_DROP_RATE", "ABS", 20, 0)],
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.UnsupportedFunction, plan.Status);
		Assert.False(plan.IsReadyForRegistry);
		var function = Assert.Single(plan.Functions);
		Assert.False(function.IsSupported);
		Assert.Equal("unsupported", function.JavaFunctionType);
		Assert.Contains("supported BufEffect stat function mapping", plan.MissingInputs);
	}

	[Fact]
	public void CreateRegistryPlan_IsReadyOnlyWhenAllRequiredLiveProvidersExist()
	{
		var plan = SkillBuffStatFunctionPlanService.CreateRegistryPlan(
			8472,
			"boostdroprate",
			1,
			[new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)],
			hasLiveEffectStatOwnerProvider: true,
			hasLiveStatFunctionRegistryProvider: true,
			hasLiveConditionValidatorProvider: true);

		Assert.Equal(SkillBuffStatFunctionRegistryPlanStatus.Ready, plan.Status);
		Assert.True(plan.IsReadyForRegistry);
		Assert.Empty(plan.MissingInputs);
	}
}
