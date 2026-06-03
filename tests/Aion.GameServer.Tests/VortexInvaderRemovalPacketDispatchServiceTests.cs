using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexInvaderRemovalPacketDispatchServiceTests
{
	[Fact]
	public async Task DispatchAsync_DisabledExecutorRecordsMessagesWithoutCallingRegistry()
	{
		var removal = CreateOnlineRemoval();
		var registry = new RecordingConnectionRegistry();
		var service = new VortexInvaderRemovalPacketDispatchService(registry, enabled: false);

		var result = await service.DispatchAsync(removal);

		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.DisabledNoSend, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.Equal([1401452, 1401474], result.Messages.Select(message => message.MessageId).ToArray());
		Assert.All(result.Messages, message =>
		{
			Assert.False(message.AttemptedSend);
			Assert.False(message.SentPacket);
			Assert.Equal(VortexInvaderRemovalPacketDispatchMessageStatus.NotAttemptedDisabled, message.Status);
		});
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task DispatchAsync_EnabledSendsRemovalMessagesToPlayerInJavaOrder()
	{
		var removal = CreateOnlineRemoval();
		var registry = new RecordingConnectionRegistry();
		var service = new VortexInvaderRemovalPacketDispatchService(registry, enabled: true);

		var result = await service.DispatchAsync(removal);

		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.Completed, result.Status);
		Assert.True(result.SendsPackets);
		Assert.True(result.IsLive);
		Assert.Equal(2, result.SentCount);
		Assert.Equal([1002, 1002], registry.SentPackets.Select(packet => packet.PlayerObjectId));
		Assert.Equal([1401452, 1401474], registry.SentPackets.Select(packet => Assert.IsType<SmSystemMessage>(packet.Packet).MessageId));
		Assert.Equal(
			[
				VortexInvaderRemovalPacketDispatchMessageStatus.Sent,
				VortexInvaderRemovalPacketDispatchMessageStatus.Sent,
			],
			result.Messages.Select(message => message.Status));
	}

	[Fact]
	public async Task DispatchAsync_EnabledStopsAfterFirstSendExceptionLikeSequentialJavaCalls()
	{
		var removal = CreateOnlineRemoval();
		var registry = new RecordingConnectionRegistry(
			exceptions: new Dictionary<int, Queue<Exception>>
			{
				[1002] = new Queue<Exception>([new InvalidOperationException("send failed")]),
			});
		var service = new VortexInvaderRemovalPacketDispatchService(registry, enabled: true);

		var result = await service.DispatchAsync(removal);

		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.Completed, result.Status);
		Assert.True(result.StopsAfterFirstFailure);
		Assert.Equal([1401452], registry.SentPackets.Select(packet => Assert.IsType<SmSystemMessage>(packet.Packet).MessageId));
		var message = Assert.Single(result.Messages);
		Assert.Equal(1401452, message.MessageId);
		Assert.True(message.AttemptedSend);
		Assert.False(message.SentPacket);
		Assert.Equal(VortexInvaderRemovalPacketDispatchMessageStatus.FailedAndStopped, message.Status);
		Assert.Equal("send failed", message.FailureReason);
	}

	[Fact]
	public async Task DispatchAsync_MissingRegistryRecordsUnsentMessages()
	{
		var removal = CreateOnlineRemoval();
		var service = new VortexInvaderRemovalPacketDispatchService(connectionRegistry: null, enabled: true);

		var result = await service.DispatchAsync(removal);

		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.MissingRegistry, result.Status);
		Assert.True(result.IsLive);
		Assert.False(result.SendsPackets);
		Assert.Equal([1401452, 1401474], result.Messages.Select(message => message.MessageId).ToArray());
		Assert.All(result.Messages, message =>
		{
			Assert.False(message.AttemptedSend);
			Assert.Equal(VortexInvaderRemovalPacketDispatchMessageStatus.MissingConnection, message.Status);
		});
	}

	[Fact]
	public async Task DispatchAsync_OnlineRemovalWithoutMessagesDoesNotCallRegistry()
	{
		var registry = new RecordingConnectionRegistry();
		var service = new VortexInvaderRemovalPacketDispatchService(registry, enabled: true);

		var result = await service.DispatchAsync(CreateOnlineRemovalWithoutMessages());

		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.NoMessages, result.Status);
		Assert.False(result.SendsPackets);
		Assert.False(result.IsLive);
		Assert.Empty(result.Messages);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task DispatchAsync_OfflineOrUnremovedResultDoesNotCallRegistry()
	{
		var registry = new RecordingConnectionRegistry();
		var service = new VortexInvaderRemovalPacketDispatchService(registry, enabled: true);

		var offline = await service.DispatchAsync(CreateOfflineRemoval());
		var unremoved = await service.DispatchAsync(CreateUnremovedResult());

		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.NoRemoval, offline.Status);
		Assert.Equal(VortexInvaderRemovalPacketDispatchStatus.NoRemoval, unremoved.Status);
		Assert.Empty(offline.Messages);
		Assert.Empty(unremoved.Messages);
		Assert.Empty(registry.SentPackets);
	}

	private static VortexInvaderRemovalResult CreateOnlineRemoval()
	{
		return new VortexInvaderRemovalResult(
			Removed: true,
			PlayerObjectId: 1002,
			LocationId: 0,
			RemovedPassedPlayer: true,
			WasOnline: true,
			WasInInvasionWorld: true,
			JavaSource: "services/vortex/Invasion.kickPlayer",
			SystemMessages:
			[
				SmSystemMessage.InvasionInvaderKick(),
				SmSystemMessage.InvasionDirectPortalOutCompulsion(),
			]);
	}

	private static VortexInvaderRemovalResult CreateOnlineRemovalWithoutMessages()
	{
		return new VortexInvaderRemovalResult(
			Removed: true,
			PlayerObjectId: 1002,
			LocationId: 0,
			RemovedPassedPlayer: true,
			WasOnline: true,
			WasInInvasionWorld: false,
			JavaSource: "services/vortex/Invasion.kickPlayer",
			SystemMessages: []);
	}

	private static VortexInvaderRemovalResult CreateOfflineRemoval()
	{
		return new VortexInvaderRemovalResult(
			Removed: true,
			PlayerObjectId: 1002,
			LocationId: 0,
			RemovedPassedPlayer: true,
			WasOnline: false,
			WasInInvasionWorld: true,
			JavaSource: "services/vortex/Invasion.kickPlayer",
			SystemMessages: []);
	}

	private static VortexInvaderRemovalResult CreateUnremovedResult()
	{
		return new VortexInvaderRemovalResult(
			Removed: false,
			PlayerObjectId: 1002,
			LocationId: 0,
			RemovedPassedPlayer: false,
			WasOnline: true,
			WasInInvasionWorld: false,
			JavaSource: "services/VortexService.removeInvaderPlayer");
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlyDictionary<int, Queue<Exception>> _exceptions;

		public RecordingConnectionRegistry(IReadOnlyDictionary<int, Queue<Exception>>? exceptions = null)
		{
			_exceptions = exceptions ?? new Dictionary<int, Queue<Exception>>();
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
			if (_exceptions.TryGetValue(playerObjectId, out var exceptions) && exceptions.TryDequeue(out var exception))
				return Task.FromException<bool>(exception);

			return Task.FromResult(true);
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
			return Task.FromResult(0);
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
