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
	private const int SelectTreeSize = 341;
	private static readonly string[] FirstSelectRoots =
	[
		"SELECT1",
		"SELECT2",
		"SELECT3",
		"SELECT4",
		"SELECT5",
		"SELECT6",
		"SELECT7",
		"SELECT8",
		"SELECT9",
		"SELECT10",
		"SELECT0",
		"SELECT_NONE",
	];

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
		[24] = "RESURRECT_PET",
		[25] = "RETRIEVE_CHAR_WAREHOUSE",
		[26] = "DEPOSIT_CHAR_WAREHOUSE",
		[27] = "RETRIEVE_ACCOUNT_WAREHOUSE",
		[28] = "DEPOSIT_ACCOUNT_WAREHOUSE",
		[29] = "QUEST_ACCEPT",
		[30] = "QUEST_REFUSE",
		[31] = "QUEST_SELECT",
		[32] = "OPEN_QUEST_WINDOW",
		[33] = "OPEN_VENDOR",
		[34] = "RESURRECT_BIND",
		[35] = "RECOVERY",
		[36] = "ENTER_PVP",
		[37] = "LEAVE_PVP",
		[38] = "OPEN_POSTBOX",
		[39] = "CHECK_USER_HAS_QUEST_ITEM",
		[40] = "DIC",
		[41] = "GIVE_ITEM_PROC",
		[42] = "REMOVE_ITEM_OPTION",
		[43] = "CHANGE_ITEM_SKIN",
		[44] = "AIRLINE_SERVICE",
		[45] = "GATHER_SKILL_LEVELUP",
		[46] = "COMBINE_SKILL_LEVELUP",
		[47] = "EXTEND_INVENTORY",
		[48] = "EXTEND_CHAR_WAREHOUSE",
		[49] = "EXTEND_ACCOUNT_WAREHOUSE",
		[50] = "LEGION_LEVELUP",
		[51] = "LEGION_CREATE_EMBLEM",
		[52] = "LEGION_CHANGE_EMBLEM",
		[53] = "OPEN_LEGION_WAREHOUSE",
		[54] = "OPEN_PERSONAL_WAREHOUSE",
		[55] = "BUY_BY_AP",
		[56] = "CLOSE_LEGION_WAREHOUSE",
		[57] = "PASS_DOORMAN",
		[58] = "COMBINE_TASK",
		[59] = "EXCHANGE_COIN",
		[60] = "SHOW_CUTSCENE",
		[61] = "EDIT_CHARACTER_ALL",
		[62] = "EDIT_CHARACTER_GENDER",
		[63] = "MATCH_MAKER",
		[64] = "MAKE_MERCENARY",
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
		[77] = "INSTANCE_PARTY_MATCH",
		[78] = "TRADE_IN",
		[79] = "GIVEUP_CRAFT_EXPERT",
		[80] = "GIVEUP_CRAFT_MASTER",
		[81] = "HOUSING_BUDDY_LIST",
		[82] = "HOUSING_RANDOM_TELEPORT",
		[83] = "HOUSING_PERSONAL_INS_TELEPORT",
		[84] = "HOUSING_PERSONAL_AUCTION",
		[85] = "HOUSING_PAY_RENT",
		[86] = "HOUSING_KICK",
		[87] = "HOUSING_CHANGE_BUILDING",
		[88] = "HOUSING_CONFIG",
		[89] = "HOUSING_GIVEUP",
		[90] = "HOUSING_CANCEL_GIVEUP",
		[91] = "HOUSING_CREATE_PERSONAL_INS",
		[92] = "FUNC_PET_H_ADOPT",
		[93] = "FUNC_PET_H_ABANDON",
		[94] = "CHARGE_ITEM_SINGLE2",
		[95] = "CHARGE_ITEM_MULTI2",
		[96] = "HOUSING_RECREATE_PERSONAL_INS",
		[97] = "HOUSING_LIKE",
		[98] = "HOUSING_SCRIPT",
		[99] = "HOUSING_GUESTBOOK",
		[100] = "TOWN_CHALLENGE",
		[101] = "AP_SELL",
		[103] = "TRADE_SELL_LIST",
		[104] = "TELEPORT_SIMPLE",
		[105] = "OPEN_INSTANCE_RECRUIT",
		[106] = "MOVE_ITEM_SKIN",
		[107] = "TRADE_IN_UPGRADE",
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
		if (ExactNames.TryGetValue(dialogActionId, out var exactName))
			return Known(dialogActionId, exactName, nameIsExact: true);

		if (dialogActionId is >= 9 and <= 22)
			return Known(dialogActionId, $"SELECTED_QUEST_REWARD{dialogActionId - 7}", nameIsExact: true);
		if (dialogActionId is >= 110 and <= 124)
			return Known(dialogActionId, $"SELECTED_QUEST_AUTO_REWARD{dialogActionId - 109}", nameIsExact: true);
		var generatedSelectName = GetGeneratedSelectName(dialogActionId);
		if (generatedSelectName != null)
			return Known(dialogActionId, generatedSelectName, nameIsExact: true);
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

	private static string? GetGeneratedSelectName(int dialogActionId)
	{
		if (dialogActionId is >= 1011 and <= 5102)
		{
			var offset = dialogActionId - 1011;
			return BuildSelectName(FirstSelectRoots[offset / SelectTreeSize], offset % SelectTreeSize);
		}

		if (dialogActionId is >= 5103 and <= 5106)
			return $"SELECT1_{dialogActionId - 5102}_5";

		if (dialogActionId is >= 6500 and <= 8204)
		{
			var offset = dialogActionId - 6500;
			return BuildSelectName($"SELECT{11 + offset / SelectTreeSize}", offset % SelectTreeSize);
		}

		return null;
	}

	private static string BuildSelectName(string rootName, int ordinal)
	{
		var name = rootName;
		AppendSelectSuffix(ref name, ordinal, remainingDepth: 4);
		return name;
	}

	private static void AppendSelectSuffix(ref string name, int ordinal, int remainingDepth)
	{
		if (ordinal == 0 || remainingDepth == 0)
			return;

		ordinal--;
		var childTreeSize = GetSelectTreeSize(remainingDepth - 1);
		for (var child = 1; child <= 4; child++)
		{
			if (ordinal < childTreeSize)
			{
				name = $"{name}_{child}";
				AppendSelectSuffix(ref name, ordinal, remainingDepth - 1);
				return;
			}

			ordinal -= childTreeSize;
		}
	}

	private static int GetSelectTreeSize(int remainingDepth)
	{
		var size = 0;
		var levelSize = 1;
		for (var level = 0; level <= remainingDepth; level++)
		{
			size += levelSize;
			levelSize *= 4;
		}

		return size;
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
