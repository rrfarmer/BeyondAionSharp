using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PeriodicInstanceRegistrationServiceTests
{
	[Fact]
	public void CreateOpeningMessageForMaskId_ReturnsJavaScheduledOpeningMessages()
	{
		Assert.Equal(1400252, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(1)?.MessageId);
		Assert.Equal(1400628, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(2)?.MessageId);
		Assert.Equal(1401398, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(3)?.MessageId);
		Assert.Equal(1401730, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(107)?.MessageId);
		Assert.Equal(1401947, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(108)?.MessageId);
		Assert.Equal(1402032, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(109)?.MessageId);
		Assert.Equal(1402192, PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(111)?.MessageId);
		Assert.Null(PeriodicInstanceRegistrationService.CreateOpeningMessageForMaskId(999));
	}

	[Fact]
	public async Task OpenRegistrationAndBroadcastAsync_SendsPlannedPacketsToOnlineLevelRangeLikeJavaWorldFanout()
	{
		var service = new PeriodicInstanceRegistrationService();
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000, minLevel: 46, maxLevel: 65)]);
		var eligible = CreatePlayer(objectId: 1001, level: 50);
		var lowLevel = CreatePlayer(objectId: 1002, level: 45);
		var registry = new RecordingConnectionRegistry([eligible, lowLevel]);
		var openingMessage = new SmSystemMessage(1401234);

		var result = await service.OpenRegistrationAndBroadcastAsync(107, autoGroups, registry, openingMessage);

		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Opened, result.Plan.Status);
		Assert.Equal(2, result.SentPackets);
		Assert.False(result.StoppedRegistrationsByMaskId);
		Assert.Collection(
			registry.SentPackets,
			delivery =>
			{
				Assert.Equal(eligible.ObjectId, delivery.PlayerObjectId);
				var packet = Assert.IsType<SmAutoGroup>(delivery.Packet);
				Assert.False(packet.IsClosed);
				Assert.Equal(SmAutoGroup.EntryIconWindowId, packet.WindowId);
			},
			delivery =>
			{
				Assert.Equal(eligible.ObjectId, delivery.PlayerObjectId);
				Assert.Equal(openingMessage, delivery.Packet);
			});
	}

	[Fact]
	public async Task CloseRegistrationAndBroadcastAsync_SendsClosePacketsBeforeStopRegistrationsLikeJava()
	{
		var service = new PeriodicInstanceRegistrationService();
		var autoGroups = new AutoGroupTable([CreateAutoGroup(108, 300120000, minLevel: 46, maxLevel: 65)]);
		var eligible = CreatePlayer(objectId: 2001, level: 50);
		var registry = new RecordingConnectionRegistry([eligible]);
		var operations = new List<string>();
		Assert.True(service.OpenRegistration(108));
		registry.OnPacketSent = delivery => operations.Add($"send:{delivery.PlayerObjectId}:{delivery.Packet.GetType().Name}");

		var result = await service.CloseRegistrationAndBroadcastAsync(
			108,
			autoGroups,
			registry,
			(maskId, _) =>
			{
				operations.Add($"stop:{maskId}");
				return ValueTask.CompletedTask;
			});

		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Closed, result.Plan.Status);
		Assert.Equal(1, result.SentPackets);
		Assert.True(result.StoppedRegistrationsByMaskId);
		var delivery = Assert.Single(registry.SentPackets);
		var packet = Assert.IsType<SmAutoGroup>(delivery.Packet);
		Assert.True(packet.IsClosed);
		Assert.Equal(["send:2001:SmAutoGroup", "stop:108"], operations);
	}

	[Fact]
	public void CreateOpenRegistrationBroadcastPlan_MutatesOnceAndSendsEntryIconPlusOpeningMessageToLevelRangeLikeJava()
	{
		var service = new PeriodicInstanceRegistrationService();
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000, minLevel: 46, maxLevel: 65)]);
		var openingMessage = new SmSystemMessage(1401234);
		var eligible = CreatePlayer(objectId: 1001, level: 50);
		var lowLevel = CreatePlayer(objectId: 1002, level: 45);
		var highLevel = CreatePlayer(objectId: 1003, level: 66);

		var plan = service.CreateOpenRegistrationBroadcastPlan(
			107,
			autoGroups,
			[eligible, lowLevel, highLevel],
			openingMessage);
		var duplicate = service.CreateOpenRegistrationBroadcastPlan(107, autoGroups, [eligible]);

		Assert.True(plan.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Opened, plan.Status);
		Assert.True(plan.HasAutoGroupData);
		Assert.False(plan.WouldStopRegistrationsByMaskId);
		Assert.True(service.IsRegistrationOpen(107));
		var broadcast = Assert.Single(plan.PlayerBroadcasts);
		Assert.Equal(eligible.ObjectId, broadcast.PlayerObjectId);
		Assert.Collection(
			broadcast.Packets,
			packet =>
			{
				var autoGroupPacket = Assert.IsType<SmAutoGroup>(packet);
				Assert.Equal(107, autoGroupPacket.MaskId);
				Assert.Equal(SmAutoGroup.EntryIconWindowId, autoGroupPacket.WindowId);
				Assert.False(autoGroupPacket.IsClosed);
			},
			packet =>
			{
				var systemMessage = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1401234, systemMessage.MessageId);
			});
		Assert.False(duplicate.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.AlreadyOpen, duplicate.Status);
		Assert.Empty(duplicate.PlayerBroadcasts);
	}

	[Fact]
	public void CreateCloseRegistrationBroadcastPlan_MutatesOnceAndMarksStopRegistrationsLikeJava()
	{
		var service = new PeriodicInstanceRegistrationService();
		var autoGroups = new AutoGroupTable([CreateAutoGroup(108, 300120000, minLevel: 46, maxLevel: 65)]);
		var eligible = CreatePlayer(objectId: 2001, level: 50);
		var lowLevel = CreatePlayer(objectId: 2002, level: 45);
		Assert.True(service.OpenRegistration(108));

		var plan = service.CreateCloseRegistrationBroadcastPlan(108, autoGroups, [eligible, lowLevel]);
		var duplicate = service.CreateCloseRegistrationBroadcastPlan(108, autoGroups, [eligible]);

		Assert.True(plan.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Closed, plan.Status);
		Assert.True(plan.HasAutoGroupData);
		Assert.True(plan.WouldStopRegistrationsByMaskId);
		Assert.False(service.IsRegistrationOpen(108));
		var broadcast = Assert.Single(plan.PlayerBroadcasts);
		Assert.Equal(eligible.ObjectId, broadcast.PlayerObjectId);
		var autoGroupPacket = Assert.IsType<SmAutoGroup>(Assert.Single(broadcast.Packets));
		Assert.Equal(108, autoGroupPacket.MaskId);
		Assert.Equal(SmAutoGroup.EntryIconWindowId, autoGroupPacket.WindowId);
		Assert.True(autoGroupPacket.IsClosed);
		Assert.False(duplicate.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.NotOpen, duplicate.Status);
		Assert.False(duplicate.WouldStopRegistrationsByMaskId);
		Assert.Empty(duplicate.PlayerBroadcasts);
	}

	[Fact]
	public void CreateRegistrationBroadcastPlan_PreservesStateChangeWhenAutoGroupDataMissingLikeJavaUnknownTypeNoBroadcast()
	{
		var service = new PeriodicInstanceRegistrationService();

		var openPlan = service.CreateOpenRegistrationBroadcastPlan(999, autoGroups: null, [CreatePlayer(level: 50)]);
		var closePlan = service.CreateCloseRegistrationBroadcastPlan(999, autoGroups: null, [CreatePlayer(level: 50)]);

		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Opened, openPlan.Status);
		Assert.False(openPlan.HasAutoGroupData);
		Assert.Empty(openPlan.PlayerBroadcasts);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Closed, closePlan.Status);
		Assert.False(closePlan.HasAutoGroupData);
		Assert.True(closePlan.WouldStopRegistrationsByMaskId);
		Assert.Empty(closePlan.PlayerBroadcasts);
	}

	[Fact]
	public void CreateOpenRegistrationPackets_FiltersByLevelAndPortalCooldownLikeJavaPeriodicInstanceManager()
	{
		var service = new PeriodicInstanceRegistrationService();
		Assert.True(service.OpenRegistration(107));
		Assert.False(service.OpenRegistration(107));
		Assert.True(service.OpenRegistration(108));
		var autoGroups = new AutoGroupTable(
		[
			CreateAutoGroup(107, 300110000, minLevel: 46, maxLevel: 65),
			CreateAutoGroup(108, 300120000, minLevel: 46, maxLevel: 65),
			CreateAutoGroup(109, 300130000, minLevel: 46, maxLevel: 65),
		]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(1, 300110000, "PC_ALL", MaxCount: 1),
			new InstanceCooltimeSummary(2, 300120000, "PC_ALL", MaxCount: 1),
		]);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var eligible = CreatePlayer(level: 50);
		var lowLevel = CreatePlayer(level: 45);
		var onCooldown = CreatePlayer(level: 50);
		onCooldown.PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
		{
			[300110000] = new(300110000, ReuseTimeMillis: 200_000, EntryCount: 1),
			[300120000] = new(300120000, ReuseTimeMillis: 200_000, EntryCount: 1),
		};

		var eligiblePackets = service.CreateOpenRegistrationPackets(eligible, autoGroups, cooltimes, now);
		var lowLevelPackets = service.CreateOpenRegistrationPackets(lowLevel, autoGroups, cooltimes, now);
		var cooldownPackets = service.CreateOpenRegistrationPackets(onCooldown, autoGroups, cooltimes, now);

		Assert.Equal([107, 108], eligiblePackets.Select(packet => packet.MaskId).OrderBy(maskId => maskId));
		Assert.Empty(lowLevelPackets);
		Assert.Empty(cooldownPackets);
		Assert.True(service.CloseRegistration(108));
		Assert.False(service.CloseRegistration(108));
		Assert.True(service.IsRegistrationOpen(107));
		Assert.False(service.IsRegistrationOpen(108));
	}

	private static AutoGroupSummary CreateAutoGroup(int maskId, int worldId, int minLevel, int maxLevel)
	{
		return new AutoGroupSummary(
			maskId,
			worldId,
			NameId: 140000 + maskId,
			TitleId: 150000 + maskId,
			minLevel,
			maxLevel,
			RegisterQuick: true,
			RegisterGroup: true,
			RegisterNew: true,
			NpcIds: []);
	}

	private static Player CreatePlayer(int level, int objectId = 1001)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Level{level}",
			Race = "ELYOS",
			Level = level,
		};
	}

	private sealed class RecordingConnectionRegistry(IReadOnlyList<Player> onlinePlayers) : IGameClientConnectionRegistry
	{
		public List<PacketDelivery> SentPackets { get; } = [];

		public Action<PacketDelivery>? OnPacketSent { get; set; }

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = onlinePlayers.FirstOrDefault(candidate => candidate.Name == playerName);
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in onlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			if (!onlinePlayers.Any(player => player.ObjectId == playerObjectId))
				return Task.FromResult(false);

			var delivery = new PacketDelivery(playerObjectId, packet);
			SentPackets.Add(delivery);
			OnPacketSent?.Invoke(delivery);
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			throw new NotSupportedException();
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			throw new NotSupportedException();
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			throw new NotSupportedException();
		}
	}

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);
}
