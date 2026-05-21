using System.Data.Common;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
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
}

public sealed class MySqlHouseAuctionRepository : IHouseAuctionRepository
{
	private readonly ILogger<MySqlHouseAuctionRepository> _logger;

	public MySqlHouseAuctionRepository(ILogger<MySqlHouseAuctionRepository> logger)
	{
		_logger = logger;
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
				.Where(group => MatchesLandRace(group, player.Race, housingTemplates, npcTemplates))
				.Select(group => group.ToSummary(housingTemplates, CalculateRemainingAuctionSeconds()))
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

	private static async Task<IReadOnlyList<HouseAuctionBidRow>> LoadBidRowsAsync(CancellationToken cancellationToken)
	{
		// Java parity: dao/HouseBidsDAO.LOAD_QUERY joined to HousesDAO-loaded address/building fields needed by SM_HOUSE_BIDS.
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT
				CASE WHEN b.player_id <> 0 AND p.id IS NULL THEN 0 ELSE b.player_id END AS player_id,
				b.house_id,
				b.bid,
				b.bid_time,
				h.address,
				h.building_id
			FROM house_bids b
				INNER JOIN houses h ON h.id = b.house_id
				LEFT JOIN players p ON p.id = b.player_id
			ORDER BY b.bid, b.bid_time
			""";

		var rows = new List<HouseAuctionBidRow>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			rows.Add(
				new HouseAuctionBidRow(
					ReadInt(reader, "player_id"),
					ReadInt(reader, "house_id"),
					ReadLong(reader, "bid"),
					ReadDateTime(reader, "bid_time"),
					ReadInt(reader, "address"),
					ReadInt(reader, "building_id")));
		}

		return rows;
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
					initialKinah: row.Kinah,
					row.BidTime);
				groupsByHouseId[row.HouseObjectId] = group;
				groups.Add(group);
				continue;
			}

			group.AddBid(row.PlayerObjectId, row.Kinah, row.BidTime);
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

	private static int CalculateRemainingAuctionSeconds()
	{
		// Java parity: taskmanager/tasks/housing/AuctionEndTask default cron, without per-house prolongation state yet.
		var now = DateTimeOffset.Now;
		var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
		var auctionEnd = new DateTimeOffset(now.Date.AddDays(daysUntilSunday).AddHours(12), now.Offset);
		if (auctionEnd <= now)
			auctionEnd = auctionEnd.AddDays(7);
		return Math.Max(0, (int)(auctionEnd - now).TotalSeconds);
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

	private static DateTime ReadDateTime(DbDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
	}

	private sealed record HouseAuctionBidRow(
		int PlayerObjectId,
		int HouseObjectId,
		long Kinah,
		DateTime BidTime,
		int AddressId,
		int BuildingId);

	private sealed class HouseAuctionBidGroup
	{
		private readonly List<HouseAuctionBid> _bids = [];

		public HouseAuctionBidGroup(
			int listIndex,
			int houseObjectId,
			int addressId,
			int buildingId,
			long initialKinah,
			DateTime initialBidTime)
		{
			ListIndex = listIndex;
			HouseObjectId = houseObjectId;
			AddressId = addressId;
			BuildingId = buildingId;
			_bids.Add(new HouseAuctionBid(ListIndex, PlayerObjectId: 0, Kinah: initialKinah, Time: initialBidTime));
		}

		public int ListIndex { get; }

		public int HouseObjectId { get; }

		public int AddressId { get; }

		public int BuildingId { get; }

		public HouseAuctionBid InitialOffer => _bids[0];

		private HouseAuctionBid HighestBid => _bids[^1];

		public void AddBid(int playerObjectId, long kinah, DateTime bidTime)
		{
			// Java parity: model/house/HouseBids.bid accepts higher bids or one real bid matching the initial offer.
			var highestBid = HighestBid;
			if (highestBid.Kinah < kinah || _bids.Count == 1 && highestBid.Kinah == kinah)
				_bids.Add(new HouseAuctionBid(ListIndex, playerObjectId, kinah, bidTime));
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
	}

	private sealed record HouseAuctionBid(int ListIndex, int PlayerObjectId, long Kinah, DateTime Time);
}
