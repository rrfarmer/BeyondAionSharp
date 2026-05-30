namespace Aion.GameServer.Services;

public sealed record ClassSelectionDialogPageIdPlan(
	string Race,
	string PlayerClass,
	int DialogPageId,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record ClassSelectionByDialogActionPlan(
	string Race,
	int DialogActionId,
	string? SelectedClass,
	bool HasValidSelection,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ClassChangeDialogPlanService
{
	// Java parity: services/ClassChangeService.getClassSelectionDialogPageId.
	// Dialog page IDs (all strings upper-case keyed, matched after ToUpperInvariant).
	private static int GetDialogPageId(string raceUpper, string classUpper)
	{
		return (raceUpper, classUpper) switch
		{
			("ELYOS", "WARRIOR") => 2375,
			("ASMODIANS", "WARRIOR") => 3057,
			("ELYOS", "SCOUT") => 2716,
			("ASMODIANS", "SCOUT") => 3398,
			("ELYOS", "MAGE") => 3057,
			("ASMODIANS", "MAGE") => 3739,
			("ELYOS", "PRIEST") => 3398,
			("ASMODIANS", "PRIEST") => 4080,
			("ELYOS", "ENGINEER") => 3739,
			("ASMODIANS", "ENGINEER") => 3569,
			("ELYOS", "ARTIST") => 4080,
			("ASMODIANS", "ARTIST") => 3910,
			_ => 0,
		};
	}

	// Java parity: services/ClassChangeService.getSelectedPlayerClass for ELYOS.
	// Dialog action IDs from Java DialogAction constants.
	private static string? GetElyosClass(int dialogActionId)
	{
		return dialogActionId switch
		{
			2376 => "GLADIATOR",    // SELECT5_1
			2461 => "TEMPLAR",      // SELECT5_2
			2717 => "ASSASSIN",     // SELECT6_1
			2802 => "RANGER",       // SELECT6_2
			3058 => "SORCERER",     // SELECT7_1
			3143 => "SPIRIT_MASTER", // SELECT7_2
			3399 => "CLERIC",       // SELECT8_1
			3484 => "CHANTER",      // SELECT8_2
			3740 => "GUNNER",       // SELECT9_1
			3825 => "RIDER",        // SELECT9_2
			4081 => "BARD",         // SELECT10_1
			_ => null,
		};
	}

	// Java parity: services/ClassChangeService.getSelectedPlayerClass for ASMODIANS.
	private static string? GetAsmodiansClass(int dialogActionId)
	{
		return dialogActionId switch
		{
			3058 => "GLADIATOR",    // SELECT7_1
			3143 => "TEMPLAR",      // SELECT7_2
			3399 => "ASSASSIN",     // SELECT8_1
			3484 => "RANGER",       // SELECT8_2
			3740 => "SORCERER",     // SELECT9_1
			3825 => "SPIRIT_MASTER", // SELECT9_2
			4081 => "CLERIC",       // SELECT10_1
			4166 => "CHANTER",      // SELECT10_2
			3570 => "GUNNER",       // SELECT8_3_1
			3591 => "RIDER",        // SELECT8_3_2
			3911 => "BARD",         // SELECT9_3_1
			_ => null,
		};
	}

	public static ClassSelectionDialogPageIdPlan CreateDialogPageIdPlan(string race, string playerClass)
	{
		var raceUpper = race.ToUpperInvariant();
		var classUpper = playerClass.ToUpperInvariant();
		var pageId = GetDialogPageId(raceUpper, classUpper);
		return new ClassSelectionDialogPageIdPlan(
			raceUpper,
			classUpper,
			pageId,
			pageId == 0
				? $"ClassChangeService.getClassSelectionDialogPageId -> default -> 0 for race={raceUpper}, class={classUpper}"
				: $"ClassChangeService.getClassSelectionDialogPageId -> {raceUpper}/{classUpper} -> {pageId}"
		);
	}

	public static ClassSelectionByDialogActionPlan CreateClassSelectionPlan(string race, int dialogActionId)
	{
		var raceUpper = race.ToUpperInvariant();
		var selectedClass = raceUpper switch
		{
			"ELYOS" => GetElyosClass(dialogActionId),
			"ASMODIANS" => GetAsmodiansClass(dialogActionId),
			_ => null,
		};
		return new ClassSelectionByDialogActionPlan(
			raceUpper,
			dialogActionId,
			selectedClass,
			HasValidSelection: selectedClass != null,
			selectedClass != null
				? $"ClassChangeService.getSelectedPlayerClass -> {raceUpper}/dialogAction={dialogActionId} -> {selectedClass}"
				: $"ClassChangeService.getSelectedPlayerClass -> {raceUpper}/dialogAction={dialogActionId} -> null (no match)"
		);
	}
}
