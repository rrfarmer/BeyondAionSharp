namespace Aion.GameServer.Model.Legion;

public sealed record LegionHistoryRow(
	int Id,
	int EpochSeconds,
	string ActionName,
	byte ActionId,
	int TypeOrdinal,
	string Name,
	string Description);

public static class LegionHistoryActions
{
	public const string KinahDeposit = "KINAH_DEPOSIT";
	public const string KinahWithdraw = "KINAH_WITHDRAW";
	public const string ItemDeposit = "ITEM_DEPOSIT";
	public const string ItemWithdraw = "ITEM_WITHDRAW";
	public const string Join = "JOIN";
	public const string Kick = "KICK";
	public const string LevelUp = "LEVEL_UP";
	public const string Appointed = "APPOINTED";
	public const string EmblemRegister = "EMBLEM_REGISTER";
	public const string EmblemModified = "EMBLEM_MODIFIED";

	public const int TypeLegion = 0;
	public const int TypeReward = 1;
	public const int TypeWarehouse = 2;

	public static bool TryGetActionMetadata(string actionName, out byte actionId, out int typeOrdinal)
	{
		// Java parity: model/team/legion/LegionHistoryAction declaration order and ids.
		(actionId, typeOrdinal) = actionName switch
		{
			"CREATE" => ((byte)0, TypeLegion),
			Join => ((byte)1, TypeLegion),
			Kick => ((byte)2, TypeLegion),
			LevelUp => ((byte)3, TypeLegion),
			Appointed => ((byte)4, TypeLegion),
			EmblemRegister => ((byte)5, TypeLegion),
			EmblemModified => ((byte)6, TypeLegion),
			"DEFENSE" => ((byte)11, TypeReward),
			"OCCUPATION" => ((byte)12, TypeReward),
			"LEGION_RENAME" => ((byte)13, TypeLegion),
			"CHARACTER_RENAME" => ((byte)14, TypeLegion),
			ItemDeposit => ((byte)15, TypeWarehouse),
			ItemWithdraw => ((byte)16, TypeWarehouse),
			KinahDeposit => ((byte)17, TypeWarehouse),
			KinahWithdraw => ((byte)18, TypeWarehouse),
			_ => ((byte)0, -1),
		};

		return typeOrdinal >= 0;
	}

	public static bool IsValidTypeOrdinal(int typeOrdinal) => typeOrdinal is TypeLegion or TypeReward or TypeWarehouse;
}
