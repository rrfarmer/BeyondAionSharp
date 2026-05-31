using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillStatConditionPreviewGoldenFixturePlanServiceTests
{
	[Fact]
	public void CreatePlan_ListsRequiredJavaRuntimeEvidence()
	{
		var plan = SkillStatConditionPreviewGoldenFixturePlanService.CreatePlan();

		Assert.False(plan.HasRuntimeGoldenEvidence);
		Assert.Equal(10, plan.FixtureCount);
		Assert.Contains("Java runtime harness for Stat2/IStatFunction condition validation", plan.MissingEvidence);
		Assert.Contains("no Java runtime/golden output has been captured", plan.ParityEvidenceLevel, StringComparison.Ordinal);
		Assert.Contains("Conditions.validate(Stat2, IStatFunction)", plan.JavaExecutionPlan, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_CoversWeaponMatchMismatchAndNonPlayerPassThrough()
	{
		var plan = SkillStatConditionPreviewGoldenFixturePlanService.CreatePlan();

		var match = Find(plan, "weapon-player-mainhand-match");
		Assert.Equal("weapon", match.ConditionSequence);
		Assert.Contains("WeaponCondition", match.JavaArtifacts);
		Assert.Contains("Player main-hand ItemGroup is ORB", match.JavaInputs);
		Assert.Contains("weapon:Satisfied", match.ExpectedConditionStatuses);
		Assert.Equal("Evaluated", match.ExpectedPurePreviewStatus);

		var mismatch = Find(plan, "weapon-player-mainhand-mismatch");
		Assert.Contains("Player main-hand ItemGroup is DAGGER", mismatch.JavaInputs);
		Assert.Contains("weapon:NotSatisfied", mismatch.ExpectedConditionStatuses);
		Assert.Equal("ConditionNotSatisfied", mismatch.ExpectedPurePreviewStatus);

		var nonPlayer = Find(plan, "weapon-non-player-pass-through");
		Assert.Contains("Stat2 owner is non-Player Creature", nonPlayer.JavaInputs);
		Assert.Contains("non-player owners", nonPlayer.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_CoversChargeAndOnFlyEdges()
	{
		var plan = SkillStatConditionPreviewGoldenFixturePlanService.CreatePlan();

		var chargeSatisfied = Find(plan, "charge-item-owner-level-satisfies");
		Assert.Contains("Item.getChargeLevel() returns 2", chargeSatisfied.JavaInputs);
		Assert.Contains("charge:Satisfied", chargeSatisfied.ExpectedConditionStatuses);

		var chargeLow = Find(plan, "charge-item-owner-level-too-low");
		Assert.Contains("Item.getChargeLevel() returns 1", chargeLow.JavaInputs);
		Assert.Contains("charge:NotSatisfied", chargeLow.ExpectedConditionStatuses);

		var nonItem = Find(plan, "charge-non-item-owner-false");
		Assert.Contains("IStatFunction owner is not Item", nonItem.JavaInputs);
		Assert.Contains("charge:NotSatisfied", nonItem.ExpectedConditionStatuses);

		var flying = Find(plan, "onfly-owner-flying");
		Assert.Contains("stat.getOwner().isFlying() returns true", flying.JavaInputs);
		Assert.Equal("Evaluated", flying.ExpectedPurePreviewStatus);

		var notFlying = Find(plan, "onfly-owner-not-flying");
		Assert.Contains("stat.getOwner().isFlying() returns false", notFlying.JavaInputs);
		Assert.Equal("ConditionNotSatisfied", notFlying.ExpectedPurePreviewStatus);
	}

	[Fact]
	public void CreatePlan_CoversPassThroughAndMixedShortCircuit()
	{
		var plan = SkillStatConditionPreviewGoldenFixturePlanService.CreatePlan();

		var front = Find(plan, "front-stat-pass-through");
		Assert.Equal("front", front.ConditionSequence);
		Assert.Contains("Condition", front.JavaArtifacts);
		Assert.Contains("front:Satisfied", front.ExpectedConditionStatuses);
		Assert.Contains("base method returns true", front.JavaSource, StringComparison.Ordinal);

		var mixed = Find(plan, "mixed-short-circuit-weapon-before-charge");
		Assert.Equal("weapon -> charge", mixed.ConditionSequence);
		Assert.Contains("weapon:NotSatisfied", mixed.ExpectedConditionStatuses);
		Assert.Contains("charge:NotEvaluated", mixed.ExpectedConditionStatuses);
		Assert.Contains("first failed child", mixed.JavaSource, StringComparison.Ordinal);
	}

	private static SkillStatConditionPreviewGoldenFixtureCase Find(
		SkillStatConditionPreviewGoldenFixturePlan plan,
		string fixtureName)
	{
		return Assert.Single(plan.Cases, testCase => string.Equals(testCase.FixtureName, fixtureName, StringComparison.Ordinal));
	}
}
