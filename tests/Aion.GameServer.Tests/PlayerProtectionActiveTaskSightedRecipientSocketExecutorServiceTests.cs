using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskSightedRecipientSocketExecutorServiceTests
{
	[Fact]
	public async Task ExecuteAsync_DisabledExecutorDoesNotCallRegistryAndRecordsRecipients()
	{
		var trace = CreateTrace();
		var registry = new RecordingConnectionRegistry();
		var service = new PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(registry, enabled: false);

		var result = await service.ExecuteAsync(trace, CreatePacket());

		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.DisabledNoSend, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.All(result.Recipients, recipient =>
		{
			Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.NotAttemptedDisabled, recipient.Status);
			Assert.False(recipient.AttemptedSend);
			Assert.False(recipient.SentPacket);
		});
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledSendsSourceFirstThenSightedRecipients()
	{
		var trace = CreateTrace();
		var registry = new RecordingConnectionRegistry();
		var service = new PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(trace, CreatePacket());

		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.Completed, result.Status);
		Assert.True(result.SendsPackets);
		Assert.True(result.IsLive);
		Assert.Equal(2, result.SentCount);
		Assert.Equal([SourcePlayerObjectId, SightedPlayerObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		Assert.All(result.Recipients, recipient =>
			Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.Sent, recipient.Status));
	}

	[Fact]
	public async Task ExecuteAsync_EnabledContinuesAfterKnownListRecipientException()
	{
		var trace = CreateTrace(includeSecondSightedRecipient: true);
		var registry = new RecordingConnectionRegistry(
			exceptions: new Dictionary<int, Exception>
			{
				[SightedPlayerObjectId] = new InvalidOperationException("known send failed"),
			});
		var service = new PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(trace, CreatePacket());

		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.Completed, result.Status);
		Assert.True(result.KnownListFailureContinuesTraversal);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.Sent,
				PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.FailedAndContinued,
				PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.Sent,
			],
			result.Recipients.Select(recipient => recipient.Status));
		Assert.Equal([SourcePlayerObjectId, SightedPlayerObjectId, SecondSightedPlayerObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId));
	}

	[Fact]
	public async Task ExecuteAsync_EnabledStopsBeforeKnownListWhenSourceSendThrows()
	{
		var trace = CreateTrace(includeSecondSightedRecipient: true);
		var registry = new RecordingConnectionRegistry(
			exceptions: new Dictionary<int, Exception>
			{
				[SourcePlayerObjectId] = new InvalidOperationException("source send failed"),
			});
		var service = new PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(trace, CreatePacket());

		Assert.True(result.SourceFailureStopsKnownListTraversal);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.FailedAndStopped,
				PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.NotAttemptedSourceFailure,
				PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.NotAttemptedSourceFailure,
			],
			result.Recipients.Select(recipient => recipient.Status));
		Assert.Equal([SourcePlayerObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId));
	}

	[Fact]
	public async Task ExecuteAsync_NoBroadcastOrNoPacketDoesNotCallRegistry()
	{
		var player = new Player { ObjectId = SourcePlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);
		var fanoutPlan = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Start);
		var trace = PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(
			fanoutPlan,
			sourceKnownListSnapshot: null,
			recipientVisibilityFacts: null);
		var registry = new RecordingConnectionRegistry();
		var service = new PlayerProtectionActiveTaskSightedRecipientSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(trace, packet: null);

		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.NoPacket, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.Empty(result.Recipients);
		Assert.Empty(registry.SentPackets);
	}

	private static PlayerProtectionActiveTaskSightedRecipientTrace CreateTrace(bool includeSecondSightedRecipient = false)
	{
		var player = new Player { ObjectId = SourcePlayerObjectId };
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);
		var fanoutPlan = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Start);
		var membershipService = new PlayerKnownListMembershipService();
		var candidates = includeSecondSightedRecipient
			? new[]
			{
				new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(SecondSightedPlayerObjectId, IsVisibleToOwner: true),
			}
			: [new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true)];
		var snapshot = membershipService.UpsertKnownPlayers(SourcePlayerObjectId, candidates);
		var facts = includeSecondSightedRecipient
			? new[]
			{
				new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true),
				new PlayerProtectionActiveTaskRecipientVisibilityFact(SecondSightedPlayerObjectId, RecipientSeesSource: true),
			}
			: [new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true)];

		return PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(fanoutPlan, snapshot, facts);
	}

	private static SmPlayerState CreatePacket()
	{
		var player = new Player { ObjectId = SourcePlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		return new SmPlayerState(player);
	}

	private const int SourcePlayerObjectId = 1001;
	private const int SightedPlayerObjectId = 1002;
	private const int SecondSightedPlayerObjectId = 1003;

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlyDictionary<int, bool> _sendResults;
		private readonly IReadOnlyDictionary<int, Exception> _exceptions;

		public RecordingConnectionRegistry(
			IReadOnlyDictionary<int, bool>? sendResults = null,
			IReadOnlyDictionary<int, Exception>? exceptions = null)
		{
			_sendResults = sendResults ?? new Dictionary<int, bool>();
			_exceptions = exceptions ?? new Dictionary<int, Exception>();
		}

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
			if (_exceptions.TryGetValue(playerObjectId, out var exception))
				return Task.FromException<bool>(exception);

			return Task.FromResult(!_sendResults.TryGetValue(playerObjectId, out var sent) || sent);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates) =>
			Task.FromResult(0);

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail) =>
			Task.FromResult(false);

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah) =>
			Task.FromResult(false);
	}
}
