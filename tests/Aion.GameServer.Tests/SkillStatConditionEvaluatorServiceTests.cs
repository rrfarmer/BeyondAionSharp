using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillStatConditionEvaluatorServiceTests
{
	[Fact]
	public void Evaluate_WeaponMatchesMainHandItemGroup()
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("weapon", ("weapon", "ORB SPELLBOOK")),
			new SkillStatConditionCreatureInputSnapshot(true, "ORB", false, [], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.Satisfied, result.Status);
		Assert.True(result.IsSatisfied);
		Assert.Contains("WeaponCondition.validate", result.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Evaluate_WeaponFailsWhenMainHandItemGroupIsNotListed()
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("weapon", ("weapon", "ORB SPELLBOOK")),
			new SkillStatConditionCreatureInputSnapshot(true, "DAGGER", false, [], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.NotSatisfied, result.Status);
	}

	[Fact]
	public void Evaluate_WeaponUsesJavaNonPlayerPassThroughRuleWhenCreatureInputIsKnown()
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("weapon", ("weapon", "ORB")),
			new SkillStatConditionCreatureInputSnapshot(false, null, false, [], "non-player owner"));

		Assert.Equal(SkillStatConditionEvaluationStatus.Satisfied, result.Status);
	}

	[Fact]
	public void Evaluate_WeaponReportsMissingAttributeAndMainHandInput()
	{
		var missingAttribute = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("weapon"),
			new SkillStatConditionCreatureInputSnapshot(true, "ORB", false, [], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.MissingInput, missingAttribute.Status);
		Assert.Contains("XML weapon attribute ItemGroup list", missingAttribute.MissingInputs);

		var missingMainHand = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("weapon", ("weapon", "ORB")),
			new SkillStatConditionCreatureInputSnapshot(true, null, false, ["equipped main-hand item"], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.MissingInput, missingMainHand.Status);
		Assert.Contains("equipped main-hand item", missingMainHand.MissingInputs);
	}

	[Theory]
	[InlineData(2, "1", SkillStatConditionEvaluationStatus.Satisfied)]
	[InlineData(1, "2", SkillStatConditionEvaluationStatus.NotSatisfied)]
	public void Evaluate_ChargeComparesJavaChargeLevelToValueAttribute(
		int chargeLevel,
		string requiredValue,
		SkillStatConditionEvaluationStatus expectedStatus)
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("charge", ("value", requiredValue)),
			itemOwnerSnapshot: new SkillStatConditionItemOwnerInputSnapshot(true, chargeLevel, [], "test"));

		Assert.Equal(expectedStatus, result.Status);
		Assert.Contains("ItemChargeCondition.validate", result.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Evaluate_ChargeReturnsFalseForNonItemOwnerLikeJava()
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("charge", ("value", "1")),
			itemOwnerSnapshot: new SkillStatConditionItemOwnerInputSnapshot(false, 0, ["IStatFunction Item owner"], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.NotSatisfied, result.Status);
		Assert.Empty(result.MissingInputs);
	}

	[Fact]
	public void Evaluate_ChargeReportsMissingSnapshotAndInvalidValue()
	{
		var missingSnapshot = SkillStatConditionEvaluatorService.Evaluate(CreateCondition("charge", ("value", "1")));

		Assert.Equal(SkillStatConditionEvaluationStatus.MissingInput, missingSnapshot.Status);
		Assert.Contains("item owner condition input snapshot", missingSnapshot.MissingInputs);

		var invalidValue = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("charge", ("value", "not-an-int")),
			itemOwnerSnapshot: new SkillStatConditionItemOwnerInputSnapshot(true, 1, [], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.MissingInput, invalidValue.Status);
		Assert.Contains("XML value attribute integer", invalidValue.MissingInputs);
	}

	[Theory]
	[InlineData(true, SkillStatConditionEvaluationStatus.Satisfied)]
	[InlineData(false, SkillStatConditionEvaluationStatus.NotSatisfied)]
	public void Evaluate_OnFlyUsesCreatureFlyingState(bool isFlying, SkillStatConditionEvaluationStatus expectedStatus)
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("onfly"),
			new SkillStatConditionCreatureInputSnapshot(true, null, isFlying, [], "test"));

		Assert.Equal(expectedStatus, result.Status);
		Assert.Contains("OnFlyCondition.validate", result.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Evaluate_OnFlyReportsMissingCreatureInput()
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(
			CreateCondition("onfly"),
			new SkillStatConditionCreatureInputSnapshot(false, null, false, ["Stat2 owner Player/Creature"], "test"));

		Assert.Equal(SkillStatConditionEvaluationStatus.MissingInput, result.Status);
		Assert.Contains("Stat2 owner Player/Creature", result.MissingInputs);
	}

	[Fact]
	public void Evaluate_UnsupportedConditionReportsUnsupportedStatus()
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(CreateCondition("unsupported_condition"));

		Assert.Equal(SkillStatConditionEvaluationStatus.UnsupportedCondition, result.Status);
		Assert.Contains("unsupported stat-condition evaluator: unsupported_condition", result.MissingInputs);
	}

	[Theory]
	[InlineData("front", "FrontCondition")]
	[InlineData("back", "BackCondition")]
	[InlineData("chargeweapon", "ChargeWeaponCondition")]
	public void Evaluate_KnownPassThroughStatConditionsUseBaseConditionValidation(
		string conditionName,
		string javaType)
	{
		var result = SkillStatConditionEvaluatorService.Evaluate(CreateCondition(conditionName));

		Assert.Equal(SkillStatConditionEvaluationStatus.Satisfied, result.Status);
		Assert.True(result.IsSatisfied);
		Assert.Empty(result.MissingInputs);
		Assert.Contains(javaType, result.JavaSource, StringComparison.Ordinal);
		Assert.Contains("Condition.validate base method returns true", result.JavaSource, StringComparison.Ordinal);
	}

	private static SkillStatChangeConditionSummary CreateCondition(
		string conditionName,
		params (string Key, string Value)[] attributes)
	{
		return new SkillStatChangeConditionSummary(
			conditionName,
			attributes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
	}
}
