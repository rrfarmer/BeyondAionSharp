using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class GroupDataExchangeFanoutSocketAdapterServiceTests
{
	[Fact]
	public async Task ExecuteAsync_DisabledNearbyBroadcastDoesNotCallRegistry()
	{
		var plan = CreateNearbyPlan();
		var registry = new RecordingConnectionRegistry();
		var service = new GroupDataExchangeFanoutSocketAdapterService(registry, enabled: false);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.DisabledNoSend, result.Status);
		Assert.True(result.WouldCallBroadcastToVisiblePlayersAsync);
		Assert.False(result.DidCallBroadcastToVisiblePlayersAsync);
		Assert.False(result.IsLive);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledNearbyBroadcastUsesVisiblePlayerRegistryWithSourceIncluded()
	{
		var plan = CreateNearbyPlan();
		var registry = new RecordingConnectionRegistry(broadcastResult: 3);
		var service = new GroupDataExchangeFanoutSocketAdapterService(registry, enabled: true);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.Completed, result.Status);
		Assert.True(result.IsLive);
		Assert.Equal(3, result.SentCount);
		var broadcast = Assert.Single(registry.Broadcasts);
		Assert.Equal(SourceObjectId, broadcast.SourceObjectId);
		Assert.True(broadcast.IncludeSourcePlayer);
		Assert.Same(plan.Packet, broadcast.Packet);
	}

	[Fact]
	public async Task ExecuteAsync_DisabledGroupFanoutRecordsRecipientsWithoutSending()
	{
		var plan = CreateGroupPlan([1002, 1003]);
		var registry = new RecordingConnectionRegistry();
		var service = new GroupDataExchangeFanoutSocketAdapterService(registry, enabled: false);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.DisabledNoSend, result.Status);
		Assert.False(result.WouldCallBroadcastToVisiblePlayersAsync);
		Assert.True(result.WouldCallSendPacketToPlayerAsync);
		Assert.False(result.DidCallSendPacketToPlayerAsync);
		Assert.False(result.IsLive);
		Assert.Equal([1002, 1003], result.RecipientResults.Select(recipient => recipient.RecipientObjectId));
		Assert.All(result.RecipientResults, recipient => Assert.Equal(GroupDataExchangeFanoutSocketRecipientStatus.NotAttemptedDisabled, recipient.Status));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledGroupFanoutSendsRecipientsInPlanOrder()
	{
		var plan = CreateGroupPlan([1002, 1003]);
		var registry = new RecordingConnectionRegistry(sendResults: new Dictionary<int, bool>
		{
			[1002] = true,
			[1003] = true,
		});
		var service = new GroupDataExchangeFanoutSocketAdapterService(registry, enabled: true);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.Completed, result.Status);
		Assert.True(result.IsLive);
		Assert.Equal(2, result.SentCount);
		Assert.True(result.DidCallSendPacketToPlayerAsync);
		Assert.Equal([1002, 1003], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		Assert.All(registry.SentPackets, sent => Assert.Same(plan.Packet, sent.Packet));
		Assert.All(result.RecipientResults, recipient => Assert.Equal(GroupDataExchangeFanoutSocketRecipientStatus.Sent, recipient.Status));
	}

	[Fact]
	public async Task ExecuteAsync_EnabledGroupFanoutMapsRegistryFalseToMissingConnection()
	{
		var plan = CreateGroupPlan([1002, 1003]);
		var registry = new RecordingConnectionRegistry(sendResults: new Dictionary<int, bool>
		{
			[1002] = true,
			[1003] = false,
		});
		var service = new GroupDataExchangeFanoutSocketAdapterService(registry, enabled: true);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(1, result.SentCount);
		Assert.Equal(
			[
				GroupDataExchangeFanoutSocketRecipientStatus.Sent,
				GroupDataExchangeFanoutSocketRecipientStatus.MissingConnection,
			],
			result.RecipientResults.Select(recipient => recipient.Status));
	}

	[Fact]
	public async Task ExecuteAsync_NoPacketPlanDoesNotCallRegistry()
	{
		var plan = GroupDataExchangeFanoutPlanService.CreatePlan(
			CreatePlayer(SourceObjectId),
			action: 1,
			groupType: 0,
			unknown2: 0,
			[],
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime(),
			new PlayerLeagueRuntime());
		var registry = new RecordingConnectionRegistry();
		var service = new GroupDataExchangeFanoutSocketAdapterService(registry, enabled: true);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.NoPacket, result.Status);
		Assert.False(result.IsLive);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledWithoutRegistryReturnsMissingRegistry()
	{
		var plan = CreateGroupPlan([1002]);
		var service = new GroupDataExchangeFanoutSocketAdapterService(enabled: true);

		var result = await service.ExecuteAsync(plan, SourcePosition);

		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.MissingRegistry, result.Status);
		Assert.True(result.IsLive);
		Assert.True(result.WouldCallSendPacketToPlayerAsync);
		Assert.False(result.DidCallSendPacketToPlayerAsync);
		Assert.Equal(GroupDataExchangeFanoutSocketRecipientStatus.MissingConnection, Assert.Single(result.RecipientResults).Status);
	}

	private const int SourceObjectId = 1001;

	private static readonly WorldPosition SourcePosition = new(210010000, 10f, 20f, 30f, 0);

	private static GroupDataExchangeFanoutPlan CreateNearbyPlan()
	{
		return GroupDataExchangeFanoutPlanService.CreatePlan(
			CreatePlayer(SourceObjectId),
			action: 1,
			groupType: 0,
			unknown2: 0,
			[1, 2, 3],
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime(),
			new PlayerLeagueRuntime());
	}

	private static GroupDataExchangeFanoutPlan CreateGroupPlan(IReadOnlyList<int> memberObjectIds)
	{
		var groupRuntime = new PlayerGroupRuntime();
		var source = CreatePlayer(SourceObjectId);
		var members = memberObjectIds.Select(CreatePlayer).ToArray();
		groupRuntime.CreateOrUpdateGroup(9001, [source, .. members]);

		return GroupDataExchangeFanoutPlanService.CreatePlan(
			source,
			action: 2,
			groupType: 0,
			unknown2: 7,
			[10, 11],
			groupRuntime,
			new PlayerAllianceRuntime(),
			new PlayerLeagueRuntime());
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Player{objectId}",
			IsOnline = true,
			Position = SourcePosition,
		};
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly int _broadcastResult;
		private readonly IReadOnlyDictionary<int, bool> _sendResults;

		public RecordingConnectionRegistry(
			int broadcastResult = 0,
			IReadOnlyDictionary<int, bool>? sendResults = null)
		{
			_broadcastResult = broadcastResult;
			_sendResults = sendResults ?? new Dictionary<int, bool>();
		}

		public List<(WorldPosition SourcePosition, int SourceObjectId, GameServerPacket Packet, bool IncludeSourcePlayer)> Broadcasts { get; } = [];

		public List<(int PlayerObjectId, GameServerPacket Packet)> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SentPackets.Add((playerObjectId, packet));
			return Task.FromResult(_sendResults.GetValueOrDefault(playerObjectId));
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			Broadcasts.Add((sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(_broadcastResult);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}
