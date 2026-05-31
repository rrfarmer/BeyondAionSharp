namespace Aion.GameServer.Services;

public static class SkillStatConditionPreviewGoldenFixturePlanService
{
	public static SkillStatConditionPreviewGoldenFixturePlan CreatePlan()
	{
		var cases = new[]
		{
			CreateCase(
				"weapon-player-mainhand-match",
				"weapon",
				["WeaponCondition"],
				["Stat2 owner is Player", "XML weapon=\"ORB SPELLBOOK\"", "Player main-hand ItemGroup is ORB"],
				["weapon:Satisfied"],
				"Evaluated",
				"WeaponCondition.validate(Stat2, IStatFunction) -> itemGroups.contains(player.getEquipment().getMainHandWeaponType())"),
			CreateCase(
				"weapon-player-mainhand-mismatch",
				"weapon",
				["WeaponCondition"],
				["Stat2 owner is Player", "XML weapon=\"ORB SPELLBOOK\"", "Player main-hand ItemGroup is DAGGER"],
				["weapon:NotSatisfied"],
				"ConditionNotSatisfied",
				"WeaponCondition.validate(Stat2, IStatFunction) returns false when player main-hand ItemGroup is not in the XML list"),
			CreateCase(
				"weapon-non-player-pass-through",
				"weapon",
				["WeaponCondition"],
				["Stat2 owner is non-Player Creature", "XML weapon=\"ORB\""],
				["weapon:Satisfied"],
				"Evaluated",
				"WeaponCondition.isValidWeapon returns true for non-player owners"),
			CreateCase(
				"front-stat-pass-through",
				"front",
				["FrontCondition", "Condition"],
				["Stat2 owner can be any Creature", "front condition attached to StatFunction"],
				["front:Satisfied"],
				"Evaluated",
				"FrontCondition does not override validate(Stat2, IStatFunction); Condition.validate base method returns true"),
			CreateCase(
				"charge-item-owner-level-satisfies",
				"charge",
				["ItemChargeCondition", "Item"],
				["IStatFunction owner is Item", "XML value=\"1\"", "Item.getChargeLevel() returns 2"],
				["charge:Satisfied"],
				"Evaluated",
				"ItemChargeCondition.validate returns item.getChargeLevel() >= value for Item owners"),
			CreateCase(
				"charge-item-owner-level-too-low",
				"charge",
				["ItemChargeCondition", "Item"],
				["IStatFunction owner is Item", "XML value=\"2\"", "Item.getChargeLevel() returns 1"],
				["charge:NotSatisfied"],
				"ConditionNotSatisfied",
				"ItemChargeCondition.validate returns false when item.getChargeLevel() is below XML value"),
			CreateCase(
				"charge-non-item-owner-false",
				"charge",
				["ItemChargeCondition"],
				["IStatFunction owner is not Item", "XML value=\"1\""],
				["charge:NotSatisfied"],
				"ConditionNotSatisfied",
				"ItemChargeCondition.validate returns false when statFunction.getOwner() is not an Item"),
			CreateCase(
				"onfly-owner-flying",
				"onfly",
				["OnFlyCondition"],
				["Stat2 owner is Creature", "stat.getOwner().isFlying() returns true"],
				["onfly:Satisfied"],
				"Evaluated",
				"OnFlyCondition.validate(Stat2, IStatFunction) returns stat.getOwner().isFlying()"),
			CreateCase(
				"onfly-owner-not-flying",
				"onfly",
				["OnFlyCondition"],
				["Stat2 owner is Creature", "stat.getOwner().isFlying() returns false"],
				["onfly:NotSatisfied"],
				"ConditionNotSatisfied",
				"OnFlyCondition.validate(Stat2, IStatFunction) returns false when owner is not flying"),
			CreateCase(
				"mixed-short-circuit-weapon-before-charge",
				"weapon -> charge",
				["Conditions", "WeaponCondition", "ItemChargeCondition"],
				["XML/list order is weapon then charge", "weapon condition returns false", "charge condition would require Item owner if reached"],
				["weapon:NotSatisfied", "charge:NotEvaluated"],
				"ConditionNotSatisfied",
				"Conditions.validate(Stat2, IStatFunction) returns false on first failed child and does not evaluate later children"),
		};

		return new SkillStatConditionPreviewGoldenFixturePlan(
			cases,
			["Java runtime harness for Stat2/IStatFunction condition validation", "Java golden output capture for each fixture"],
			"Plan only - no Java runtime/golden output has been captured",
			"Use Java source-of-truth condition classes to run each fixture against Conditions.validate(Stat2, IStatFunction), then compare C# isolated preview status and current value");
	}

	private static SkillStatConditionPreviewGoldenFixtureCase CreateCase(
		string fixtureName,
		string conditionSequence,
		IReadOnlyList<string> javaArtifacts,
		IReadOnlyList<string> javaInputs,
		IReadOnlyList<string> expectedConditionStatuses,
		string expectedPurePreviewStatus,
		string javaSource)
	{
		return new SkillStatConditionPreviewGoldenFixtureCase(
			fixtureName,
			conditionSequence,
			javaArtifacts,
			javaInputs,
			expectedConditionStatuses,
			expectedPurePreviewStatus,
			javaSource);
	}
}

public sealed record SkillStatConditionPreviewGoldenFixturePlan(
	IReadOnlyList<SkillStatConditionPreviewGoldenFixtureCase> Cases,
	IReadOnlyList<string> MissingEvidence,
	string ParityEvidenceLevel,
	string JavaExecutionPlan)
{
	public int FixtureCount => Cases.Count;

	public bool HasRuntimeGoldenEvidence => MissingEvidence.Count == 0;
}

public sealed record SkillStatConditionPreviewGoldenFixtureCase(
	string FixtureName,
	string ConditionSequence,
	IReadOnlyList<string> JavaArtifacts,
	IReadOnlyList<string> JavaInputs,
	IReadOnlyList<string> ExpectedConditionStatuses,
	string ExpectedPurePreviewStatus,
	string JavaSource);
