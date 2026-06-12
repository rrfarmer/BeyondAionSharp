using System.Data.Common;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IHouseAuctionRepository
{
	Task<HouseAuctionBidPage> LoadHouseBidsAsync(
		Player player,
		HousingTemplateTable? housingTemplates,
		NpcTemplateTable? npcTemplates,
		CancellationToken cancellationToken = default);

	Task<HouseAuctionRegistrationResult> RegisterHouseAuctionAsync(
		int playerObjectId,
		int houseObjectId,
		long initialBidKinah,
		InventoryItem kinahItem,
		DateTime bidTime,
		CancellationToken cancellationToken = default);

	Task<HouseAuctionBidContext?> LoadHouseBidContextAsync(
		int playerObjectId,
		int listIndex,
		HousingTemplateTable? housingTemplates,
		CancellationToken cancellationToken = default);

	Task<HouseAuctionPlaceBidResult> PlaceHouseBidAsync(
		int playerObjectId,
		int listIndex,
		long bidOffer,
		InventoryItem kinahItem,
		int refundMailObjectId,
		DateTime bidTime,
		HousingTemplateTable? housingTemplates,
		CancellationToken cancellationToken = default);

	Task<bool> PayHouseRentAsync(
		int playerObjectId,
		int houseObjectId,
		DateTime nextPay,
		InventoryItem kinahItem,
		CancellationToken cancellationToken = default);

	Task<bool> UpdateHouseSettingsAsync(
		int playerObjectId,
		int houseObjectId,
		int settings,
		string? signNotice,
		CancellationToken cancellationToken = default);
}

public enum HouseAuctionRegistrationResult
{
	Success,
	AlreadyRegistered,
	Failed,
}

public sealed class EmptyHouseAuctionRepository : IHouseAuctionRepository
{
	public Task<HouseAuctionBidPage> LoadHouseBidsAsync(
		Player player,
		HousingTemplateTable? housingTemplates,
		NpcTemplateTable? npcTemplates,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(HouseAuctionBidPage.Empty);
	}

	public Task<HouseAuctionRegistrationResult> RegisterHouseAuctionAsync(
		int playerObjectId,
		int houseObjectId,
		long initialBidKinah,
		InventoryItem kinahItem,
		DateTime bidTime,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(HouseAuctionRegistrationResult.Failed);
	}

	public Task<HouseAuctionBidContext?> LoadHouseBidContextAsync(
		int playerObjectId,
		int listIndex,
		HousingTemplateTable? housingTemplates,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult<HouseAuctionBidContext?>(null);
	}

	public Task<HouseAuctionPlaceBidResult> PlaceHouseBidAsync(
		int playerObjectId,
		int listIndex,
		long bidOffer,
		InventoryItem kinahItem,
		int refundMailObjectId,
		DateTime bidTime,
		HousingTemplateTable? housingTemplates,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new HouseAuctionPlaceBidResult(HouseAuctionPlaceBidStatus.Failed));
	}

	public Task<bool> PayHouseRentAsync(
		int playerObjectId,
		int houseObjectId,
		DateTime nextPay,
		InventoryItem kinahItem,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> UpdateHouseSettingsAsync(
		int playerObjectId,
		int houseObjectId,
		int settings,
		string? signNotice,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed class MySqlHouseAuctionRepository : IHouseAuctionRepository
{
	private readonly ILogger<MySqlHouseAuctionRepository> _logger;
	private readonly HouseAuctionTimingService _auctionTiming;

	public MySqlHouseAuctionRepository(ILogger<MySqlHouseAuctionRepository> logger, HouseAuctionTimingService? auctionTiming = null)
	{
		_logger = logger;
		_auctionTiming = auctionTiming ?? new HouseAuctionTimingService();
	}

	public async Task<HouseAuctionBidPage> LoadHouseBidsAsync(
		Player player,
		HousingTemplateTable? housingTemplates,
		NpcTemplateTable? npcTemplates,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingBidService.getBidInfo(player.getRace()) plus SM_HOUSE_BIDS last/registered headers.
		try
		{
			var rows = await LoadBidRowsAsync(cancellationToken);
			var groups = BuildBidGroups(rows);
			var lastBid = groups
				.Select(group => group.GetLatestBid(player.ObjectId))
				.Where(bid => bid != null)
				.OrderByDescending(bid => bid!.Time)
				.FirstOrDefault();
			var registeredHouse = FindRegisteredHouseBid(groups, player.Houses);
			var visibleBids = groups
				.Where(group => MatchesLandRace(group, player.Race.ToString(), housingTemplates, npcTemplates))
				.Select(group => group.ToSummary(housingTemplates, _auctionTiming.GetRemainingAuctionSeconds(group.HouseObjectId)))
				.ToArray();

			return new HouseAuctionBidPage(
				visibleBids,
				lastBid?.ListIndex ?? 0,
				lastBid?.Kinah ?? 0,
				registeredHouse?.ListIndex ?? 0,
				registeredHouse?.InitialOffer.Kinah ?? 0);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load housing auction bids for player {PlayerObjectId}", player.ObjectId);
			return HouseAuctionBidPage.Empty;
		}
	}

	public async Task<HouseAuctionRegistrationResult> RegisterHouseAuctionAsync(
		int playerObjectId,
		int houseObjectId,
		long initialBidKinah,
		InventoryItem kinahItem,
		DateTime bidTime,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingBidService.auction with HouseBidsDAO.addBid plus Inventory.tryDecreaseKinah caller state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await PlayerOwnsHouseAsync(connection, transaction, playerObjectId, houseObjectId, cancellationToken))
				return HouseAuctionRegistrationResult.Failed;
			if (await HouseHasBidsAsync(connection, transaction, houseObjectId, cancellationToken))
				return HouseAuctionRegistrationResult.AlreadyRegistered;
			if (!await UpdateKinahAsync(connection, transaction, kinahItem, cancellationToken))
				return HouseAuctionRegistrationResult.Failed;

			await InsertInitialBidAsync(connection, transaction, houseObjectId, initialBidKinah, bidTime, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return HouseAuctionRegistrationResult.Success;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not register house {HouseObjectId} for auction by player {PlayerObjectId}", houseObjectId, playerObjectId);
			return HouseAuctionRegistrationResult.Failed;
		}
	}

	public async Task<HouseAuctionBidContext?> LoadHouseBidContextAsync(
		int playerObjectId,
		int listIndex,
		HousingTemplateTable? housingTemplates,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingBidService.bid locates the target HouseBids by client listIndex.
		try
		{
			var groups = BuildBidGroups(await LoadBidRowsAsync(cancellationToken));
			var target = groups.FirstOrDefault(group => group.ListIndex == listIndex);
			return target?.ToContext(
				housingTemplates,
				groups.Any(group => group.ListIndex != listIndex && group.HighestBid.PlayerObjectId == playerObjectId));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load housing bid context for player {PlayerObjectId} and list index {ListIndex}", playerObjectId, listIndex);
			return null;
		}
	}

	public async Task<HouseAuctionPlaceBidResult> PlaceHouseBidAsync(
		int playerObjectId,
		int listIndex,
		long bidOffer,
		InventoryItem kinahItem,
		int refundMailObjectId,
		DateTime bidTime,
		HousingTemplateTable? housingTemplates,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingBidService.bid success branch with HouseBidsDAO.addBid and previous-bid refund mail.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			var groups = BuildBidGroups(await LoadBidRowsAsync(connection, transaction, forUpdate: true, cancellationToken));
			var target = groups.FirstOrDefault(group => group.ListIndex == listIndex);
			if (target == null)
				return new HouseAuctionPlaceBidResult(HouseAuctionPlaceBidStatus.Missing);
			if (!target.CanAcceptBid(bidOffer))
				return new HouseAuctionPlaceBidResult(HouseAuctionPlaceBidStatus.PriceChanged, target.AddressId);
			if (!await UpdateKinahAsync(connection, transaction, kinahItem, cancellationToken))
				return new HouseAuctionPlaceBidResult(HouseAuctionPlaceBidStatus.Failed, target.AddressId);

			var previousBid = target.HighestBid;
			await InsertBidAsync(connection, transaction, playerObjectId, target.HouseObjectId, bidOffer, bidTime, cancellationToken);
			var refundMail = await TryCreatePreviousBidRefundMailAsync(
				connection,
				transaction,
				previousBid,
				target,
				refundMailObjectId,
				bidTime,
				cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return new HouseAuctionPlaceBidResult(
				HouseAuctionPlaceBidStatus.Success,
				target.AddressId,
				kinahItem,
				refundMail,
				refundMail?.RecipientId ?? 0);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not place house bid for player {PlayerObjectId} and list index {ListIndex}", playerObjectId, listIndex);
			return new HouseAuctionPlaceBidResult(HouseAuctionPlaceBidStatus.Failed);
		}
	}

	public async Task<bool> PayHouseRentAsync(
		int playerObjectId,
		int houseObjectId,
		DateTime nextPay,
		InventoryItem kinahItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_HOUSE_PAY_RENT persists House.nextPay and Storage.decreaseKinah.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await UpdateKinahAsync(connection, transaction, kinahItem, cancellationToken))
				return false;
			if (!await UpdateHouseNextPayAsync(connection, transaction, playerObjectId, houseObjectId, nextPay, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not pay rent for house {HouseObjectId} by player {PlayerObjectId}", houseObjectId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> UpdateHouseSettingsAsync(
		int playerObjectId,
		int houseObjectId,
		int settings,
		string? signNotice,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_HOUSE_SETTINGS mutates House permissions/sign notice; House.save stores settings/sign_notice.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE houses SET settings = ?, sign_notice = ? WHERE id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = settings },
					new MySqlParameter { Value = (object?)signNotice ?? DBNull.Value },
					new MySqlParameter { Value = houseObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not update settings for house {HouseObjectId} by player {PlayerObjectId}", houseObjectId, playerObjectId);
			return false;
		}
	}

	private static Task<IReadOnlyList<HouseAuctionBidRow>> LoadBidRowsAsync(CancellationToken cancellationToken)
	{
		return LoadBidRowsAsync(connection: null, transaction: null, forUpdate: false, cancellationToken);
	}

	private static async Task<IReadOnlyList<HouseAuctionBidRow>> LoadBidRowsAsync(
		DbConnection? connection,
		DbTransaction? transaction,
		bool forUpdate,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/HouseBidsDAO.LOAD_QUERY joined to HousesDAO-loaded address/building fields needed by SM_HOUSE_BIDS.
		var ownsConnection = connection == null;
		DbConnection? ownedConnection = null;
		connection ??= DatabaseFactory.GetConnection();
		if (ownsConnection)
			ownedConnection = connection;
		if (ownsConnection)
			await connection.OpenAsync(cancellationToken);

		try
		{
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = $"""
				SELECT
					CASE WHEN b.player_id <> 0 AND p.id IS NULL THEN 0 ELSE b.player_id END AS player_id,
					p.name AS player_name,
					p.race AS player_race,
					b.house_id,
					b.bid,
					b.bid_time,
					h.address,
					h.building_id,
					h.player_id AS house_owner_id
				FROM house_bids b
					INNER JOIN houses h ON h.id = b.house_id
					LEFT JOIN players p ON p.id = b.player_id
				ORDER BY b.bid, b.bid_time
				{(forUpdate ? "FOR UPDATE" : string.Empty)}
				""";

			var rows = new List<HouseAuctionBidRow>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				rows.Add(
					new HouseAuctionBidRow(
						ReadInt(reader, "player_id"),
						ReadString(reader, "player_name"),
						ReadString(reader, "player_race"),
						ReadInt(reader, "house_id"),
						ReadLong(reader, "bid"),
						ReadDateTime(reader, "bid_time"),
						ReadInt(reader, "address"),
						ReadInt(reader, "building_id"),
						ReadInt(reader, "house_owner_id")));
			}

			return rows;
		}
		finally
		{
			if (ownedConnection != null)
				await ownedConnection.DisposeAsync();
		}
	}

	private static IReadOnlyList<HouseAuctionBidGroup> BuildBidGroups(IReadOnlyList<HouseAuctionBidRow> rows)
	{
		// Java parity: dao/HouseBidsDAO.loadBids creates HouseBids on first seen house and calls HouseBids.bid for later rows.
		var groupsByHouseId = new Dictionary<int, HouseAuctionBidGroup>();
		var groups = new List<HouseAuctionBidGroup>();
		foreach (var row in rows)
		{
			if (!groupsByHouseId.TryGetValue(row.HouseObjectId, out var group))
			{
				group = new HouseAuctionBidGroup(
					listIndex: groups.Count + 1,
					row.HouseObjectId,
					row.AddressId,
					row.BuildingId,
					row.HouseOwnerObjectId,
					initialKinah: row.Kinah,
					row.BidTime);
				groupsByHouseId[row.HouseObjectId] = group;
				groups.Add(group);
				continue;
			}

			group.AddBid(row.PlayerObjectId, row.PlayerName, row.PlayerRace, row.Kinah, row.BidTime);
		}

		return groups;
	}

	private static async Task<bool> PlayerOwnsHouseAsync(
		DbConnection connection,
		DbTransaction transaction,
		int playerObjectId,
		int houseObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: CM_REGISTER_HOUSE uses player.getActiveHouse from HousingService-loaded ownership state.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT 1 FROM houses WHERE id = ? AND player_id = ? LIMIT 1";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = houseObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken);
	}

	private static async Task<bool> HouseHasBidsAsync(
		DbConnection connection,
		DbTransaction transaction,
		int houseObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: CM_REGISTER_HOUSE denies when house.getBids() is already present.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT 1 FROM house_bids WHERE house_id = ? LIMIT 1";
		command.Parameters.Add(new MySqlParameter { Value = houseObjectId });
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken);
	}

	private static async Task<bool> UpdateKinahAsync(
		DbConnection connection,
		DbTransaction transaction,
		InventoryItem kinahItem,
		CancellationToken cancellationToken)
	{
		// Java parity: model/items/storage/Storage.tryDecreaseKinah persists the Kinah item count.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET item_count = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = kinahItem.Count },
				new MySqlParameter { Value = kinahItem.ObjectId },
				new MySqlParameter { Value = kinahItem.OwnerId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private static async Task InsertInitialBidAsync(
		DbConnection connection,
		DbTransaction transaction,
		int houseObjectId,
		long initialBidKinah,
		DateTime bidTime,
		CancellationToken cancellationToken)
	{
		// Java parity: HouseBidsDAO.INSERT_QUERY for the initial player_id=0 offer.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "INSERT INTO house_bids (player_id, house_id, bid, bid_time) VALUES (?, ?, ?, ?)";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = 0 },
				new MySqlParameter { Value = houseObjectId },
				new MySqlParameter { Value = initialBidKinah },
				new MySqlParameter { Value = bidTime },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task InsertBidAsync(
		DbConnection connection,
		DbTransaction transaction,
		int playerObjectId,
		int houseObjectId,
		long bidOffer,
		DateTime bidTime,
		CancellationToken cancellationToken)
	{
		// Java parity: HouseBidsDAO.INSERT_QUERY for a player bid.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "INSERT INTO house_bids (player_id, house_id, bid, bid_time) VALUES (?, ?, ?, ?)";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = playerObjectId },
				new MySqlParameter { Value = houseObjectId },
				new MySqlParameter { Value = bidOffer },
				new MySqlParameter { Value = bidTime },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<bool> UpdateHouseNextPayAsync(
		DbConnection connection,
		DbTransaction transaction,
		int playerObjectId,
		int houseObjectId,
		DateTime nextPay,
		CancellationToken cancellationToken)
	{
		// Java parity: model/house/House.save for the next_pay field after CM_HOUSE_PAY_RENT.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE houses SET next_pay = ? WHERE id = ? AND player_id = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = nextPay },
				new MySqlParameter { Value = houseObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private static async Task<PlayerMail?> TryCreatePreviousBidRefundMailAsync(
		DbConnection connection,
		DbTransaction transaction,
		HouseAuctionBid previousBid,
		HouseAuctionBidGroup target,
		int refundMailObjectId,
		DateTime bidTime,
		CancellationToken cancellationToken)
	{
		// Java parity: MailFormatter.sendHouseAuctionMail(..., AuctionResult.FAILED_BID, bid.getTime(), previousBid.getKinah()).
		if (previousBid.IsInitialOffer || previousBid.PlayerObjectId == 0 || refundMailObjectId == 0)
			return null;

		var recipient = await LoadPlayerMailTargetAsync(connection, transaction, previousBid.PlayerObjectId, cancellationToken);
		if (recipient == null)
			return null;

		var mail = new PlayerMail(
			refundMailObjectId,
			previousBid.PlayerObjectId,
			"$$HS_AUCTION_MAIL",
			$"0,{GetRaceId(recipient.Value.Race)}",
			$"{new DateTimeOffset(bidTime).ToUnixTimeSeconds()},{target.AddressId}",
			true,
			0,
			0,
			previousBid.Kinah,
			0,
			bidTime);
		await InsertSystemMailAsync(connection, transaction, mail, cancellationToken);
		return mail;
	}

	private static async Task<PlayerMailTarget?> LoadPlayerMailTargetAsync(
		DbConnection connection,
		DbTransaction transaction,
		int playerObjectId,
		CancellationToken cancellationToken)
	{
		// Java parity: services/player/PlayerService.getOrLoadPlayerCommonData for auction refund mail.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT name, race, mailbox_letters FROM players WHERE id = ? LIMIT 1";
		command.Parameters.Add(new MySqlParameter { Value = playerObjectId });
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return null;

		return new PlayerMailTarget(
			ReadString(reader, "name"),
			ReadString(reader, "race"),
			ReadInt(reader, "mailbox_letters"));
	}

	private static async Task InsertSystemMailAsync(
		DbConnection connection,
		DbTransaction transaction,
		PlayerMail mail,
		CancellationToken cancellationToken)
	{
		// Java parity: services/mail/SystemMailService.sendMail stores the Letter and increments offline mailbox count.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO mail (
				mail_unique_id, mail_recipient_id, sender_name, mail_title, mail_message, unread,
				attached_item_id, attached_kinah_count, expressed, recieved_time
			)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = mail.Id },
				new MySqlParameter { Value = mail.RecipientId },
				new MySqlParameter { Value = mail.SenderName },
				new MySqlParameter { Value = mail.Title },
				new MySqlParameter { Value = mail.Message },
				new MySqlParameter { Value = mail.IsUnread },
				new MySqlParameter { Value = 0 },
				new MySqlParameter { Value = mail.AttachedKinah },
				new MySqlParameter { Value = mail.LetterType },
				new MySqlParameter { Value = mail.ReceivedTime },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);

		await using var mailboxCommand = connection.CreateCommand();
		mailboxCommand.Transaction = transaction;
		mailboxCommand.CommandText = "UPDATE players SET mailbox_letters = LEAST(mailbox_letters + 1, 255) WHERE id = ?";
		mailboxCommand.Parameters.Add(new MySqlParameter { Value = mail.RecipientId });
		await mailboxCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private static HouseAuctionBidGroup? FindRegisteredHouseBid(
		IReadOnlyList<HouseAuctionBidGroup> groups,
		IReadOnlyList<PlayerHouse> playerHouses)
	{
		// Java parity: services/HousingBidService.findBidsForRegisteredHouse loops player.getHouses().
		var groupsByHouseId = groups.ToDictionary(group => group.HouseObjectId);
		foreach (var house in playerHouses)
		{
			if (groupsByHouseId.TryGetValue(house.ObjectId, out var group))
				return group;
		}

		return null;
	}

	private static bool MatchesLandRace(
		HouseAuctionBidGroup group,
		string race,
		HousingTemplateTable? housingTemplates,
		NpcTemplateTable? npcTemplates)
	{
		// Java parity: model/house/House.matchesLandRace derives Elyos lands from manager NPC tribe GENERAL.
		var address = housingTemplates?.GetAddress(group.AddressId);
		var managerNpc = address == null ? null : npcTemplates?.GetNpcTemplate(address.ManagerNpcId);
		if (managerNpc == null)
			return true;

		var isElyosLand = string.Equals(managerNpc.Tribe, "GENERAL", StringComparison.OrdinalIgnoreCase);
		return string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase) && isElyosLand
			|| string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase) && !isElyosLand;
	}

	private static int ReadInt(DbDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static long ReadLong(DbDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
	}

	private static string ReadString(DbDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}

	private static DateTime ReadDateTime(DbDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
	}

	private static int GetRaceId(string race)
	{
		// Java parity: model/Race.getRaceId for playable race system-mail params.
		return string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	private sealed record HouseAuctionBidRow(
		int PlayerObjectId,
		string PlayerName,
		string PlayerRace,
		int HouseObjectId,
		long Kinah,
		DateTime BidTime,
		int AddressId,
		int BuildingId,
		int HouseOwnerObjectId);

	private readonly record struct PlayerMailTarget(string Name, string Race, int MailboxLetters);

	private sealed class HouseAuctionBidGroup
	{
		private readonly List<HouseAuctionBid> _bids = [];

		public HouseAuctionBidGroup(
			int listIndex,
			int houseObjectId,
			int addressId,
			int buildingId,
			int ownerObjectId,
			long initialKinah,
			DateTime initialBidTime)
		{
			ListIndex = listIndex;
			HouseObjectId = houseObjectId;
			AddressId = addressId;
			BuildingId = buildingId;
			OwnerObjectId = ownerObjectId;
			_bids.Add(new HouseAuctionBid(ListIndex, PlayerObjectId: 0, PlayerName: string.Empty, PlayerRace: string.Empty, Kinah: initialKinah, Time: initialBidTime, IsInitialOffer: true));
		}

		public int ListIndex { get; }

		public int HouseObjectId { get; }

		public int AddressId { get; }

		public int BuildingId { get; }

		public int OwnerObjectId { get; }

		public HouseAuctionBid InitialOffer => _bids[0];

		public HouseAuctionBid HighestBid => _bids[^1];

		public void AddBid(int playerObjectId, string playerName, string playerRace, long kinah, DateTime bidTime)
		{
			// Java parity: model/house/HouseBids.bid accepts higher bids or one real bid matching the initial offer.
			var highestBid = HighestBid;
			if (highestBid.Kinah < kinah || _bids.Count == 1 && highestBid.Kinah == kinah)
				_bids.Add(new HouseAuctionBid(ListIndex, playerObjectId, playerName, playerRace, kinah, bidTime, IsInitialOffer: false));
		}

		public bool CanAcceptBid(long bidOffer)
		{
			// Java parity: model/house/HouseBids.bid return-null branch.
			return HighestBid.Kinah < bidOffer || _bids.Count == 1 && HighestBid.Kinah == bidOffer;
		}

		public HouseAuctionBid? GetLatestBid(int playerObjectId)
		{
			// Java parity: model/house/HouseBids.getLatestBid.
			for (var i = _bids.Count - 1; i >= 0; i--)
			{
				if (_bids[i].PlayerObjectId == playerObjectId)
					return _bids[i];
			}

			return null;
		}

		public HouseAuctionBidSummary ToSummary(HousingTemplateTable? housingTemplates, int remainingAuctionSeconds)
		{
			var address = housingTemplates?.GetAddress(AddressId);
			return new HouseAuctionBidSummary(
				ListIndex,
				HouseObjectId,
				address?.LandId ?? 0,
				AddressId,
				BuildingId,
				housingTemplates?.GetHouseTypeId(BuildingId) ?? 0,
				HighestBid.Kinah,
				_bids.Count - 1,
				remainingAuctionSeconds);
		}

		public HouseAuctionBidContext ToContext(HousingTemplateTable? housingTemplates, bool playerIsHighestBidderElsewhere)
		{
			var address = housingTemplates?.GetAddress(AddressId);
			return new HouseAuctionBidContext(
				ListIndex,
				HouseObjectId,
				OwnerObjectId,
				AddressId,
				BuildingId,
				housingTemplates?.GetHouseTypeId(BuildingId) ?? 0,
				address?.MinLevel ?? 0,
				HighestBid.Kinah,
				HighestBid.PlayerObjectId,
				InitialOffer.Kinah,
				HighestBid.IsInitialOffer,
				playerIsHighestBidderElsewhere);
		}
	}

	private sealed record HouseAuctionBid(
		int ListIndex,
		int PlayerObjectId,
		string PlayerName,
		string PlayerRace,
		long Kinah,
		DateTime Time,
		bool IsInitialOffer);
}
