using Aion.Commons.Database;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface ICustomLevelRewardRepository
{
	Task<int> LoadReceivingPlayerAsync(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		CancellationToken cancellationToken = default);

	Task<bool> StoreReceivingPlayerAsync(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		int playerObjectId,
		CancellationToken cancellationToken = default);
}

public sealed class EmptyCustomLevelRewardRepository : ICustomLevelRewardRepository
{
	public Task<int> LoadReceivingPlayerAsync(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(int.MaxValue);
	}

	public Task<bool> StoreReceivingPlayerAsync(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		int playerObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlCustomLevelRewardRepository : ICustomLevelRewardRepository
{
	private readonly ILogger<MySqlCustomLevelRewardRepository> _logger;

	public MySqlCustomLevelRewardRepository(ILogger<MySqlCustomLevelRewardRepository> logger)
	{
		_logger = logger;
	}

	public async Task<int> LoadReceivingPlayerAsync(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		CancellationToken cancellationToken = default)
	{
		var plan = CustomLevelRewardReceiptRepositoryPlan.CreateLoad(kind, accountId);
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = plan.Sql;
			foreach (var parameter in plan.Parameters)
				command.Parameters.Add(new MySqlParameter { Value = parameter });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				return reader.GetInt32("receiving_player");

			return 0;
		}
		catch (Exception ex)
		{
			// Java parity: BonusPackDAO/FactionPackDAO return Integer.MAX_VALUE on load failure.
			_logger.LogError(ex, "Could not load {RewardKind} custom reward receiver for account {AccountId}", kind, accountId);
			return int.MaxValue;
		}
	}

	public async Task<bool> StoreReceivingPlayerAsync(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		int playerObjectId,
		CancellationToken cancellationToken = default)
	{
		var plan = CustomLevelRewardReceiptRepositoryPlan.CreateStore(kind, accountId, playerObjectId);
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = plan.Sql;
			foreach (var parameter in plan.Parameters)
				command.Parameters.Add(new MySqlParameter { Value = parameter });

			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not store {RewardKind} custom reward receiver {PlayerObjectId} for account {AccountId}",
				kind,
				playerObjectId,
				accountId);
			return false;
		}
	}
}

public sealed record CustomLevelRewardReceiptRepositoryPlan(
	CustomLevelRewardReceiptKind Kind,
	CustomLevelRewardReceiptRepositoryAction Action,
	int AccountId,
	int? PlayerObjectId,
	string Sql,
	IReadOnlyList<int> Parameters,
	string JavaSource)
{
	private const string BonusLoadSql = "SELECT `receiving_player` FROM `bonus_packs` WHERE `account_id`=?";
	private const string BonusStoreSql = "REPLACE INTO `bonus_packs` (`account_id`, `receiving_player`) VALUES (?,?)";
	private const string FactionLoadSql = "SELECT `receiving_player` FROM `faction_packs` WHERE `account_id`=?";
	private const string FactionStoreSql = "REPLACE INTO `faction_packs` (`account_id`, `receiving_player`) VALUES (?,?)";

	public static CustomLevelRewardReceiptRepositoryPlan CreateLoad(
		CustomLevelRewardReceiptKind kind,
		int accountId)
	{
		return new CustomLevelRewardReceiptRepositoryPlan(
			kind,
			CustomLevelRewardReceiptRepositoryAction.LoadReceivingPlayer,
			accountId,
			PlayerObjectId: null,
			Sql: kind == CustomLevelRewardReceiptKind.Bonus ? BonusLoadSql : FactionLoadSql,
			Parameters: [accountId],
			JavaSource: kind == CustomLevelRewardReceiptKind.Bonus
				? "game-server/src/com/aionemu/gameserver/dao/BonusPackDAO.java#loadReceivingPlayer"
				: "game-server/src/com/aionemu/gameserver/dao/FactionPackDAO.java#loadReceivingPlayer");
	}

	public static CustomLevelRewardReceiptRepositoryPlan CreateStore(
		CustomLevelRewardReceiptKind kind,
		int accountId,
		int playerObjectId)
	{
		return new CustomLevelRewardReceiptRepositoryPlan(
			kind,
			CustomLevelRewardReceiptRepositoryAction.StoreReceivingPlayer,
			accountId,
			playerObjectId,
			Sql: kind == CustomLevelRewardReceiptKind.Bonus ? BonusStoreSql : FactionStoreSql,
			Parameters: [accountId, playerObjectId],
			JavaSource: kind == CustomLevelRewardReceiptKind.Bonus
				? "game-server/src/com/aionemu/gameserver/dao/BonusPackDAO.java#storeReceivingPlayer"
				: "game-server/src/com/aionemu/gameserver/dao/FactionPackDAO.java#storeReceivingPlayer");
	}
}

public enum CustomLevelRewardReceiptKind
{
	Bonus,
	Faction,
}

public enum CustomLevelRewardReceiptRepositoryAction
{
	LoadReceivingPlayer,
	StoreReceivingPlayer,
}
