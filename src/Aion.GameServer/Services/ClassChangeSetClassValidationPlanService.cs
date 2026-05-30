namespace Aion.GameServer.Services;

public enum ClassChangeSetClassValidationPlanStatus
{
	Valid,
	SkippedValidationDisabled,
	BlockedNullNewClass,
	BlockedAlreadySwitchedClass,
	BlockedInvalidClassChoice,
}

public sealed record ClassChangeSetClassValidationPlan(
	ClassChangeSetClassValidationPlanStatus Status,
	string? OldClass,
	string? NewClass,
	bool IsValid,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ClassChangeSetClassValidationPlanService
{
	// Java parity: model/PlayerClass class IDs (first constructor argument).
	// Starting classes: WARRIOR=0, SCOUT=3, MAGE=6, PRIEST=9, ENGINEER=12, ARTIST=15.
	// Subclasses: each has ID = startingClassId + 1 or startingClassId + 2.
	private static readonly IReadOnlyDictionary<string, int> ClassIds =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["WARRIOR"] = 0,
			["GLADIATOR"] = 1,
			["TEMPLAR"] = 2,
			["SCOUT"] = 3,
			["ASSASSIN"] = 4,
			["RANGER"] = 5,
			["MAGE"] = 6,
			["SORCERER"] = 7,
			["SPIRIT_MASTER"] = 8,
			["PRIEST"] = 9,
			["CLERIC"] = 10,
			["CHANTER"] = 11,
			["ENGINEER"] = 12,
			["RIDER"] = 13,
			["GUNNER"] = 14,
			["ARTIST"] = 15,
			["BARD"] = 16,
		};

	// Java parity: model/PlayerClass.isStartingClass returns true for WARRIOR, SCOUT, MAGE, PRIEST, ENGINEER, ARTIST.
	private static readonly IReadOnlySet<string> StartingClasses =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"WARRIOR", "SCOUT", "MAGE", "PRIEST", "ENGINEER", "ARTIST",
		};

	public static ClassChangeSetClassValidationPlan CreatePlan(string? oldClass, string? newClass, bool validate)
	{
		// Java parity: services/ClassChangeService.setClass(Player, PlayerClass, boolean validate, boolean updateDaevaStatus).
		if (newClass == null)
		{
			return new ClassChangeSetClassValidationPlan(
				ClassChangeSetClassValidationPlanStatus.BlockedNullNewClass,
				oldClass,
				newClass,
				IsValid: false,
				"ClassChangeService.setClass -> newClass == null -> return false"
			);
		}

		if (!validate)
		{
			return new ClassChangeSetClassValidationPlan(
				ClassChangeSetClassValidationPlanStatus.SkippedValidationDisabled,
				oldClass,
				newClass,
				IsValid: true,
				"ClassChangeService.setClass -> validate == false -> skip validation"
			);
		}

		if (oldClass != null && !IsStartingClass(oldClass))
		{
			return new ClassChangeSetClassValidationPlan(
				ClassChangeSetClassValidationPlanStatus.BlockedAlreadySwitchedClass,
				oldClass,
				newClass,
				IsValid: false,
				$"ClassChangeService.setClass -> !oldClass.isStartingClass() ({oldClass}) -> \"You already switched class\""
			);
		}

		var oldId = oldClass != null && ClassIds.TryGetValue(oldClass, out var oid) ? oid : -1;
		var newId = ClassIds.TryGetValue(newClass, out var nid) ? nid : -1;

		var isSameClass = string.Equals(oldClass, newClass, StringComparison.OrdinalIgnoreCase);
		var isOutOfRange = newId <= oldId || newId > oldId + 2;

		if (isSameClass || isOutOfRange)
		{
			return new ClassChangeSetClassValidationPlan(
				ClassChangeSetClassValidationPlanStatus.BlockedInvalidClassChoice,
				oldClass,
				newClass,
				IsValid: false,
				$"ClassChangeService.setClass -> oldClass==newClass || newId({newId}) out of range ({oldId}+1..{oldId}+2) -> \"Invalid class chosen\""
			);
		}

		return new ClassChangeSetClassValidationPlan(
			ClassChangeSetClassValidationPlanStatus.Valid,
			oldClass,
			newClass,
			IsValid: true,
			$"ClassChangeService.setClass -> valid transition {oldClass}({oldId}) -> {newClass}({newId})"
		);
	}

	private static bool IsStartingClass(string playerClass)
	{
		return StartingClasses.Contains(playerClass);
	}
}
