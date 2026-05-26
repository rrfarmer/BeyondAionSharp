using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKnownListFanoutSocketExecutorServiceTests
{
	[Fact]
	public async Task ExecuteAsync_DisabledExecutorDoesNotCallRegistryAndRecordsRecipients()
	{
		var executionPlan = CreateExecutionPlan();
		var registry = new RecordingConnectionRegistry();
		var service = new BindPointTeleportKnownListFanoutSocketExecutorService(registry, enabled: false);

		var result = await service.ExecuteAsync(executionPlan);

		Assert.Equal(BindPointTeleportKnownListFanoutSocketExecutorStatus.DisabledNoSend, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.All(result.Recipients, recipient =>
		{
			Assert.Equal(BindPointTeleportKnownListFanoutSocketRecipientStatus.NotAttemptedDisabled, recipient.Status);
			Assert.False(recipient.AttemptedSend);
		});
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task ExecuteAsync_EnabledSendsSourceFirstThenKnownListRecipientsAndSkipsOfflinePolicyRecipients()
	{
		var executionPlan = CreateExecutionPlan(onlinePlayerObjectIds: [SourcePlayerObjectId, KnownVisiblePlayerObjectId]);
		var registry = new RecordingConnectionRegistry(sendResults: new Dictionary<int, bool>
		{
			[SourcePlayerObjectId] = true,
			[KnownVisiblePlayerObjectId] = true,
		});
		var service = new BindPointTeleportKnownListFanoutSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(executionPlan);

		Assert.Equal(BindPointTeleportKnownListFanoutSocketExecutorStatus.Completed, result.Status);
		Assert.True(result.SendsPackets);
		Assert.True(result.IsLive);
		Assert.Equal(2, result.SentCount);
		Assert.Equal([SourcePlayerObjectId, KnownVisiblePlayerObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		Assert.Equal(
			[
				BindPointTeleportKnownListFanoutSocketRecipientStatus.Sent,
				BindPointTeleportKnownListFanoutSocketRecipientStatus.Sent,
				BindPointTeleportKnownListFanoutSocketRecipientStatus.SkippedOffline,
			],
			result.Recipients.Select(recipient => recipient.Status));
	}

	[Fact]
	public async Task ExecuteAsync_EnabledContinuesAfterKnownListRecipientException()
	{
		var executionPlan = CreateExecutionPlan();
		var registry = new RecordingConnectionRegistry(
			sendResults: new Dictionary<int, bool>
			{
				[SourcePlayerObjectId] = true,
				[KnownInvisiblePlayerObjectId] = true,
			},
			exceptions: new Dictionary<int, Exception>
			{
				[KnownVisiblePlayerObjectId] = new InvalidOperationException("known send failed"),
			});
		var service = new BindPointTeleportKnownListFanoutSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(executionPlan);

		Assert.Equal(BindPointTeleportKnownListFanoutSocketExecutorStatus.Completed, result.Status);
		Assert.True(result.KnownListFailureContinuesTraversal);
		Assert.Equal(
			[
				BindPointTeleportKnownListFanoutSocketRecipientStatus.Sent,
				BindPointTeleportKnownListFanoutSocketRecipientStatus.FailedAndContinued,
				BindPointTeleportKnownListFanoutSocketRecipientStatus.Sent,
			],
			result.Recipients.Select(recipient => recipient.Status));
		Assert.Equal([SourcePlayerObjectId, KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId));
	}

	[Fact]
	public async Task ExecuteAsync_EnabledStopsBeforeKnownListWhenSourceSendThrows()
	{
		var executionPlan = CreateExecutionPlan();
		var registry = new RecordingConnectionRegistry(
			exceptions: new Dictionary<int, Exception>
			{
				[SourcePlayerObjectId] = new InvalidOperationException("source send failed"),
			});
		var service = new BindPointTeleportKnownListFanoutSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(executionPlan);

		Assert.True(result.SourceFailureStopsKnownListTraversal);
		Assert.Equal(
			[
				BindPointTeleportKnownListFanoutSocketRecipientStatus.FailedAndStopped,
				BindPointTeleportKnownListFanoutSocketRecipientStatus.NotAttemptedDisabled,
				BindPointTeleportKnownListFanoutSocketRecipientStatus.NotAttemptedDisabled,
			],
			result.Recipients.Select(recipient => recipient.Status));
		Assert.Equal([SourcePlayerObjectId], registry.SentPackets.Select(packet => packet.PlayerObjectId));
	}

	[Fact]
	public async Task ExecuteAsync_NoPacketPlanReturnsNoPacketWithoutRegistryCall()
	{
		var executionPlan = BindPointTeleportKnownListFanoutExecutionPlanService.CreateDisabledPlan(
			fanoutPlan: null,
			membershipSnapshot: null,
			onlinePlayerObjectIds: [SourcePlayerObjectId]);
		var registry = new RecordingConnectionRegistry();
		var service = new BindPointTeleportKnownListFanoutSocketExecutorService(registry, enabled: true);

		var result = await service.ExecuteAsync(executionPlan);

		Assert.Equal(BindPointTeleportKnownListFanoutSocketExecutorStatus.NoPacket, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.Empty(result.Recipients);
		Assert.Empty(registry.SentPackets);
	}

	private static BindPointTeleportKnownListFanoutExecutionPlan CreateExecutionPlan(
		IEnumerable<int>? onlinePlayerObjectIds = null)
	{
		var membership = new PlayerKnownListMembershipService();
		var snapshot = membership.UpsertKnownPlayers(
			SourcePlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(KnownVisiblePlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownInvisiblePlayerObjectId, IsVisibleToOwner: false),
			]);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		return BindPointTeleportKnownListFanoutExecutionPlanService.CreateDisabledPlan(
			fanoutPlan,
			snapshot,
			onlinePlayerObjectIds ?? [SourcePlayerObjectId, KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId]);
	}

	private const int SourcePlayerObjectId = 8901;
	private const int KnownVisiblePlayerObjectId = 8902;
	private const int KnownInvisiblePlayerObjectId = 8903;
	private const int LocId = 6501;
	private const int CooldownSeconds = 600;

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
