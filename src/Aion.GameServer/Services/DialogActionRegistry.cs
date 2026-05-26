namespace Aion.GameServer.Services;

public sealed record DialogActionNameResult(
	int DialogActionId,
	string? Name,
	bool IsKnown,
	bool NameIsExact,
	string JavaSource,
	bool IsLive = false);

public static class DialogActionRegistry
{
	private static readonly IReadOnlyDictionary<int, string> ExactNames = new Dictionary<int, string>
	{
		[-1] = "USE_OBJECT",
		[1] = "NULL",
		[2] = "BUY",
		[3] = "SELL",
		[4] = "OPEN_STIGMA_WINDOW",
		[5] = "CREATE_LEGION",
		[6] = "DISPERSE_LEGION",
		[7] = "RECREATE_LEGION",
		[8] = "SELECTED_QUEST_REWARD1",
		[23] = "SELECTED_QUEST_NOREWARD",
		[26] = "DEPOSIT_CHAR_WAREHOUSE",
		[29] = "QUEST_ACCEPT",
		[30] = "QUEST_REFUSE",
		[31] = "QUEST_SELECT",
		[32] = "OPEN_QUEST_WINDOW",
		[33] = "OPEN_VENDOR",
		[35] = "RECOVERY",
		[36] = "ENTER_PVP",
		[37] = "LEAVE_PVP",
		[44] = "AIRLINE_SERVICE",
		[45] = "GATHER_SKILL_LEVELUP",
		[46] = "COMBINE_SKILL_LEVELUP",
		[47] = "EXTEND_INVENTORY",
		[48] = "EXTEND_CHAR_WAREHOUSE",
		[53] = "OPEN_LEGION_WAREHOUSE",
		[56] = "CLOSE_LEGION_WAREHOUSE",
		[58] = "COMBINE_TASK",
		[59] = "EXCHANGE_COIN",
		[61] = "EDIT_CHARACTER_ALL",
		[62] = "EDIT_CHARACTER_GENDER",
		[63] = "MATCH_MAKER",
		[65] = "INSTANCE_ENTRY",
		[66] = "COMPOUND_WEAPON",
		[67] = "DECOMPOUND_WEAPON",
		[68] = "FACTION_JOIN",
		[69] = "FACTION_SEPARATE",
		[70] = "BUY_AGAIN",
		[71] = "FUNC_PET_ADOPT",
		[72] = "FUNC_PET_ABANDON",
		[73] = "HOUSING_BUILD",
		[74] = "HOUSING_DESTRUCT",
		[75] = "CHARGE_ITEM_SINGLE",
		[76] = "CHARGE_ITEM_MULTI",
		[78] = "TRADE_IN",
		[79] = "GIVEUP_CRAFT_EXPERT",
		[80] = "GIVEUP_CRAFT_MASTER",
		[84] = "HOUSING_PERSONAL_AUCTION",
		[92] = "FUNC_PET_H_ADOPT",
		[93] = "FUNC_PET_H_ABANDON",
		[94] = "CHARGE_ITEM_SINGLE2",
		[95] = "CHARGE_ITEM_MULTI2",
		[96] = "HOUSING_RECREATE_PERSONAL_INS",
		[100] = "TOWN_CHALLENGE",
		[103] = "TRADE_SELL_LIST",
		[105] = "OPEN_INSTANCE_RECRUIT",
		[108] = "SELECTED_QUEST_AUTO_REWARD",
		[109] = "ITEM_UPGRADE",
		[125] = "OPEN_STIGMA_ENCHANT",
		[1000] = "CUSTOM1",
		[1001] = "CUSTOM2",
		[1002] = "QUEST_ACCEPT_1",
		[1003] = "QUEST_REFUSE_1",
		[1004] = "QUEST_REFUSE_2",
		[1005] = "QUEST_REFUSE_3",
		[1006] = "QUEST_REFUSE_4",
		[1007] = "ASK_QUEST_ACCEPT",
		[1008] = "FINISH_DIALOG",
		[1009] = "SELECT_QUEST_REWARD",
		[10255] = "SET_SUCCEED",
		[20000] = "QUEST_ACCEPT_SIMPLE",
		[20001] = "QUEST_REFUSE_SIMPLE",
		[20002] = "CHECK_USER_HAS_QUEST_ITEM_SIMPLE",
		[20003] = "SETPRO_NEXT",
		[20004] = "CHECK_AP",
		[20005] = "CHECK_GOLD",
		[20006] = "SELECT_BOSS_LEVEL1",
		[20007] = "SELECT_BOSS_LEVEL2",
		[20008] = "SELECT_BOSS_LEVEL3",
		[20009] = "SELECT_BOSS_LEVEL4",
		[20010] = "SELECT_BOSS_LEVEL5",
		[100000] = "OPEN_WEB",
		[100001] = "OPEN_WEB_SHOP",
	};

	public static DialogActionNameResult NameOf(int dialogActionId)
	{
		// Java parity breadcrumb: model/DialogAction.nameOf builds a map from public integer fields.
		// The large generated SELECT*/SETPRO* ranges are recognized for gating, but only the
		// fixed names above are exact string parity so far.
		if (ExactNames.TryGetValue(dialogActionId, out var exactName))
			return Known(dialogActionId, exactName, nameIsExact: true);

		if (dialogActionId is >= 9 and <= 22)
			return Known(dialogActionId, $"SELECTED_QUEST_REWARD{dialogActionId - 7}", nameIsExact: true);
		if (dialogActionId is >= 110 and <= 124)
			return Known(dialogActionId, $"SELECTED_QUEST_AUTO_REWARD{dialogActionId - 109}", nameIsExact: true);
		if (IsKnownFixedActionWithoutExactName(dialogActionId))
			return Known(dialogActionId, $"DIALOG_ACTION_{dialogActionId}", nameIsExact: false);
		if (dialogActionId is >= 1011 and <= 8204)
			return Known(dialogActionId, $"SELECT_RANGE_{dialogActionId}", nameIsExact: false);
		if (dialogActionId is >= 10000 and <= 10254)
			return Known(dialogActionId, $"SETPRO{dialogActionId - 9999}", nameIsExact: true);

		return new DialogActionNameResult(
			dialogActionId,
			Name: null,
			IsKnown: false,
			NameIsExact: false,
			"DialogAction.nameOf -> null for id not present in reflected public fields",
			IsLive: false);
	}

	private static bool IsKnownFixedActionWithoutExactName(int dialogActionId)
	{
		// Java has individual public constants for these sparse ids before SELECT1.
		return dialogActionId is >= 1 and <= 125 and not 102
			|| dialogActionId is 107
			|| dialogActionId is >= 1000 and <= 1009;
	}

	private static DialogActionNameResult Known(int dialogActionId, string name, bool nameIsExact)
	{
		return new DialogActionNameResult(
			dialogActionId,
			name,
			IsKnown: true,
			nameIsExact,
			"DialogAction.nameOf -> reflected public integer field map",
			IsLive: false);
	}
}
