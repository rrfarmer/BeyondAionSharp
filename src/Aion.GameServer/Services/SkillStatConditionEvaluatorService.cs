using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class SkillStatConditionEvaluatorService
{
	public static SkillStatConditionEvaluationResult Evaluate(
		SkillStatChangeConditionSummary condition,
		SkillStatConditionCreatureInputSnapshot? creatureSnapshot = null,
		SkillStatConditionItemOwnerInputSnapshot? itemOwnerSnapshot = null)
	{
		return condition.ConditionName switch
		{
			"weapon" => EvaluateWeapon(condition, creatureSnapshot),
			"charge" => EvaluateCharge(condition, itemOwnerSnapshot),
			"onfly" => EvaluateOnFly(condition, creatureSnapshot),
			_ when IsKnownStatPassThroughCondition(condition.ConditionName) => EvaluateKnownPassThrough(condition.ConditionName),
			_ => new SkillStatConditionEvaluationResult(
				condition.ConditionName,
				SkillStatConditionEvaluationStatus.UnsupportedCondition,
				[$"unsupported stat-condition evaluator: {condition.ConditionName}"],
				"Only audited Stat2 condition overrides and mapped base-pass-through stat conditions are implemented in this isolated helper"),
		};
	}

	private static SkillStatConditionEvaluationResult EvaluateWeapon(
		SkillStatChangeConditionSummary condition,
		SkillStatConditionCreatureInputSnapshot? creatureSnapshot)
	{
		const string javaSource = "WeaponCondition.validate(Stat2, IStatFunction) -> isValidWeapon(stat.getOwner()); Player owners require itemGroups.contains(player.getEquipment().getMainHandWeaponType()), non-player owners return true";
		if (creatureSnapshot == null)
			return Missing(condition.ConditionName, "creature condition input snapshot", javaSource);

		if (!creatureSnapshot.HasPlayerOwner)
		{
			return creatureSnapshot.MissingInputs.Count > 0
				? Missing(condition.ConditionName, creatureSnapshot.MissingInputs, javaSource)
				: new SkillStatConditionEvaluationResult(condition.ConditionName, SkillStatConditionEvaluationStatus.Satisfied, [], javaSource);
		}

		if (!condition.Attributes.TryGetValue("weapon", out var weaponAttribute) || string.IsNullOrWhiteSpace(weaponAttribute))
			return Missing(condition.ConditionName, "XML weapon attribute ItemGroup list", javaSource);

		if (string.IsNullOrWhiteSpace(creatureSnapshot.MainHandWeaponItemGroup))
			return Missing(condition.ConditionName, creatureSnapshot.MissingInputs.Count == 0 ? ["Player equipment main-hand weapon ItemGroup"] : creatureSnapshot.MissingInputs, javaSource);

		var allowedGroups = weaponAttribute.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		var status = allowedGroups.Contains(creatureSnapshot.MainHandWeaponItemGroup, StringComparer.Ordinal)
			? SkillStatConditionEvaluationStatus.Satisfied
			: SkillStatConditionEvaluationStatus.NotSatisfied;
		return new SkillStatConditionEvaluationResult(condition.ConditionName, status, [], javaSource);
	}

	private static SkillStatConditionEvaluationResult EvaluateCharge(
		SkillStatChangeConditionSummary condition,
		SkillStatConditionItemOwnerInputSnapshot? itemOwnerSnapshot)
	{
		const string javaSource = "ItemChargeCondition.validate(Stat2, IStatFunction) -> statFunction.getOwner() must be Item and item.getChargeLevel() >= value; non-Item owners return false";
		if (itemOwnerSnapshot == null)
			return Missing(condition.ConditionName, "item owner condition input snapshot", javaSource);

		if (!condition.Attributes.TryGetValue("value", out var valueAttribute)
			|| !int.TryParse(valueAttribute, out var requiredChargeLevel))
			return Missing(condition.ConditionName, "XML value attribute integer", javaSource);

		if (!itemOwnerSnapshot.HasItemOwner)
			return new SkillStatConditionEvaluationResult(condition.ConditionName, SkillStatConditionEvaluationStatus.NotSatisfied, [], javaSource);

		var status = itemOwnerSnapshot.ChargeLevel >= requiredChargeLevel
			? SkillStatConditionEvaluationStatus.Satisfied
			: SkillStatConditionEvaluationStatus.NotSatisfied;
		return new SkillStatConditionEvaluationResult(condition.ConditionName, status, [], javaSource);
	}

	private static SkillStatConditionEvaluationResult EvaluateOnFly(
		SkillStatChangeConditionSummary condition,
		SkillStatConditionCreatureInputSnapshot? creatureSnapshot)
	{
		const string javaSource = "OnFlyCondition.validate(Stat2, IStatFunction) -> stat.getOwner().isFlying()";
		if (creatureSnapshot == null)
			return Missing(condition.ConditionName, "creature condition input snapshot", javaSource);
		if (!creatureSnapshot.HasPlayerOwner && creatureSnapshot.MissingInputs.Count > 0)
			return Missing(condition.ConditionName, creatureSnapshot.MissingInputs, javaSource);

		var status = creatureSnapshot.IsFlying
			? SkillStatConditionEvaluationStatus.Satisfied
			: SkillStatConditionEvaluationStatus.NotSatisfied;
		return new SkillStatConditionEvaluationResult(condition.ConditionName, status, [], javaSource);
	}

	private static SkillStatConditionEvaluationResult EvaluateKnownPassThrough(string conditionName)
	{
		return new SkillStatConditionEvaluationResult(
			conditionName,
			SkillStatConditionEvaluationStatus.Satisfied,
			[],
			$"{MapKnownPassThroughJavaType(conditionName)} does not override validate(Stat2, IStatFunction); Condition.validate base method returns true for stat-function validation");
	}

	private static bool IsKnownStatPassThroughCondition(string conditionName)
	{
		return conditionName is
			"abnormal" or
			"back" or
			"chain" or
			"chargearmor" or
			"chargeweapon" or
			"combatcheck" or
			"dp" or
			"form" or
			"front" or
			"hp" or
			"lefthandweapon" or
			"move_casting" or
			"mp" or
			"noflying" or
			"polishchargeweapon" or
			"race" or
			"ride_robot" or
			"selfflying" or
			"skillcharge" or
			"target" or
			"targetflying";
	}

	private static string MapKnownPassThroughJavaType(string conditionName)
	{
		return conditionName switch
		{
			"abnormal" => "AbnormalStateCondition",
			"back" => "BackCondition",
			"chain" => "ChainCondition",
			"chargearmor" => "ChargeArmorCondition",
			"chargeweapon" => "ChargeWeaponCondition",
			"combatcheck" => "CombatCheckCondition",
			"dp" => "DpCondition",
			"form" => "FormCondition",
			"front" => "FrontCondition",
			"hp" => "HpCondition",
			"lefthandweapon" => "LeftHandCondition",
			"move_casting" => "PlayerMovedCondition",
			"mp" => "MpCondition",
			"noflying" => "NoFlyingCondition",
			"polishchargeweapon" => "PolishChargeCondition",
			"race" => "RaceCondition",
			"ride_robot" => "RideRobotCondition",
			"selfflying" => "SelfFlyingCondition",
			"skillcharge" => "SkillChargeCondition",
			"target" => "TargetCondition",
			"targetflying" => "TargetFlyingCondition",
			_ => "Condition",
		};
	}

	private static SkillStatConditionEvaluationResult Missing(
		string conditionName,
		string missingInput,
		string javaSource)
	{
		return Missing(conditionName, [missingInput], javaSource);
	}

	private static SkillStatConditionEvaluationResult Missing(
		string conditionName,
		IReadOnlyList<string> missingInputs,
		string javaSource)
	{
		return new SkillStatConditionEvaluationResult(
			conditionName,
			SkillStatConditionEvaluationStatus.MissingInput,
			missingInputs,
			javaSource);
	}
}

public enum SkillStatConditionEvaluationStatus
{
	Satisfied,
	NotSatisfied,
	MissingInput,
	UnsupportedCondition,
}

public sealed record SkillStatConditionEvaluationResult(
	string ConditionName,
	SkillStatConditionEvaluationStatus Status,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsSatisfied => Status == SkillStatConditionEvaluationStatus.Satisfied;
}
