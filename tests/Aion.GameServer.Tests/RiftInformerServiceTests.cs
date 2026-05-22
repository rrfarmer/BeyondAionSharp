using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class RiftInformerServiceTests
{
	[Fact]
	public async Task GetAnnounceData_CountsVortexNormalAndVolatileMasterRifts()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-informer-counts-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (service, informer) = await CreateServicesAsync(
				tempPath,
				"""
				<rift_location id="1170" world="110070000" />
				<rift_location id="2176" world="210070000" has_spawns="true" />
				<rift_location id="2177" world="210070000" has_spawns="true" />
				""",
				"""
				<spawn_map map_id="110070000">
					<rift_spawn id="1170" world="110070000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="120080000">
					<rift_spawn id="1170" world="120080000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="KAISINEL_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="210070000">
					<rift_spawn id="2176" world="210070000">
						<spawn npc_id="730100">
							<spot x="11" y="12" z="13" anchor="CYGNEA_GM" />
						</spawn>
					</rift_spawn>
					<rift_spawn id="2177" world="210070000">
						<spawn npc_id="730100">
							<spot x="21" y="22" z="23" anchor="CYGNEA_HM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="220080000">
					<rift_spawn id="2176" world="220080000">
						<spawn npc_id="730101">
							<spot x="15" y="16" z="17" anchor="ENSHAR_GS" />
						</spawn>
					</rift_spawn>
					<rift_spawn id="2177" world="220080000">
						<spawn npc_id="730101">
							<spot x="25" y="26" z="27" anchor="ENSHAR_HS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			Assert.True(service.OpenRifts(1170, guards: false).Succeeded);
			Assert.True(service.OpenRifts(2176, guards: false).Succeeded);
			Assert.True(service.OpenRifts(2177, guards: true).Succeeded);

			var academy = informer.GetAnnounceData(110070000);
			var cygnea = informer.GetAnnounceData(210070000);

			Assert.Equal(12, academy.Counts.Count);
			Assert.Equal(1, academy[1]);
			Assert.Equal(0, academy[0]);
			Assert.Equal(1, cygnea[0]);
			Assert.Equal(1, cygnea[4]);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task GetAnnounceData_IgnoresSlaveWorldAndInvasionAggregate()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-informer-invasion-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (service, informer) = await CreateServicesAsync(
				tempPath,
				"""<rift_location id="2189" world="210070000" auto_closeable="false" />""",
				"""
				<spawn_map map_id="210070000">
					<rift_spawn id="2189" world="210070000">
						<spawn npc_id="730100">
							<spot x="31" y="32" z="33" anchor="CYGNEA_VIL1M" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="220080000">
					<rift_spawn id="2189" world="220080000">
						<spawn npc_id="730101">
							<spot x="35" y="36" z="37" anchor="ENSHAR_VIL1S" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""");
			Assert.True(service.OpenRifts(2189, guards: false).Succeeded);

			var masterWorld = informer.GetAnnounceData(210070000);
			var slaveWorld = informer.GetAnnounceData(220080000);

			Assert.All(masterWorld.Counts, count => Assert.Equal(0, count));
			Assert.All(slaveWorld.Counts, count => Assert.Equal(0, count));
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public void GetTwinId_ReturnsJavaRiftWorldPairs()
	{
		var informer = new RiftInformerService(CreateEmptyRiftService());

		Assert.Equal(220020000, informer.GetTwinId(210020000));
		Assert.Equal(210020000, informer.GetTwinId(220020000));
		Assert.Equal(220080000, informer.GetTwinId(210070000));
		Assert.Equal(0, informer.GetTwinId(400010000));
	}

	[Fact]
	public async Task SendRiftsInfoAsync_WithWorldId_BroadcastsCurrentPortalPacketsAndTwinAggregatePackets()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-informer-world-fanout-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var now = DateTimeOffset.FromUnixTimeSeconds(1000);
			var registry = new RecordingConnectionRegistry();
			registry.Players.Add(CreatePlayer(100, 210020000));
			registry.Players.Add(CreatePlayer(101, 220020000));
			registry.Players.Add(CreatePlayer(102, 210040000));
			var (service, informer) = await CreateServicesAsync(
				tempPath,
				"""<rift_location id="2120" world="210020000" />""",
				"""
				<spawn_map map_id="210020000">
					<rift_spawn id="2120" world="210020000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="ELTNEN_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="220020000">
					<rift_spawn id="2120" world="220020000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="MORHEIM_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""",
				registry,
				serviceClock: () => now,
				informerClock: () => now);
			Assert.True(service.OpenRifts(2120, guards: false).Succeeded);

			var sent = await informer.SendRiftsInfoAsync(210020000);

			Assert.Equal(4, sent);
			Assert.Equal([100, 100, 100, 101], registry.BroadcastDeliveries.Select(delivery => delivery.Player.ObjectId).ToArray());
			Assert.Equal([0, 2, 3, 0], registry.BroadcastDeliveries.Select(delivery => ReadAction(delivery.Packet)).ToArray());
			Assert.Equal(1, ReadAggregateCounts(registry.BroadcastDeliveries[0].Packet)[0]);
			Assert.All(ReadAggregateCounts(registry.BroadcastDeliveries[3].Packet), count => Assert.Equal(0, count));

			var portal = Assert.Single(service.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var detail = ReadPortalDetailPayload(registry.BroadcastDeliveries[1].Packet);
			Assert.Equal(portal.MasterNpc.ObjectId, detail.ObjectId);
			Assert.Equal(portal.MaxEntries, detail.MaxEntries);
			Assert.Equal(3600, detail.RemainTime);
			Assert.Equal(portal.MinLevel, detail.MinLevel);
			Assert.Equal(portal.MaxLevel, detail.MaxLevel);
			Assert.Equal(portal.MasterNpc.Position.X, detail.X);
			Assert.Equal(portal.MasterNpc.Position.Y, detail.Y);
			Assert.Equal(portal.MasterNpc.Position.Z, detail.Z);
			Assert.Equal(0, detail.RiftType);
			Assert.Equal(1, detail.Display);

			var update = ReadPortalEntryUpdatePayload(registry.BroadcastDeliveries[2].Packet);
			Assert.Equal(portal.MasterNpc.ObjectId, update.ObjectId);
			Assert.Equal(0, update.UsedEntries);
			Assert.Equal(3600, update.RemainTime);
			Assert.Equal(0, update.RiftType);
			Assert.Equal(0, update.Unknown);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task SendRiftsInfoAsync_WithPlayer_SendsCurrentPacketsToPlayerAndTwinPacketsToTwinWorld()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-informer-player-fanout-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var now = DateTimeOffset.FromUnixTimeSeconds(1000);
			var player = CreatePlayer(100, 210020000);
			var registry = new RecordingConnectionRegistry();
			registry.Players.Add(player);
			registry.Players.Add(CreatePlayer(101, 220020000));
			var (service, informer) = await CreateServicesAsync(
				tempPath,
				"""<rift_location id="2120" world="210020000" />""",
				"""
				<spawn_map map_id="210020000">
					<rift_spawn id="2120" world="210020000">
						<spawn npc_id="730100">
							<spot x="1" y="2" z="3" anchor="ELTNEN_AM" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				<spawn_map map_id="220020000">
					<rift_spawn id="2120" world="220020000">
						<spawn npc_id="730101">
							<spot x="5" y="6" z="7" anchor="MORHEIM_AS" />
						</spawn>
					</rift_spawn>
				</spawn_map>
				""",
				registry,
				serviceClock: () => now,
				informerClock: () => now);
			Assert.True(service.OpenRifts(2120, guards: false).Succeeded);

			var sent = await informer.SendRiftsInfoAsync(player);

			Assert.Equal(4, sent);
			Assert.Equal([100, 100, 100], registry.DirectDeliveries.Select(delivery => delivery.PlayerObjectId).ToArray());
			Assert.Equal([0, 2, 3], registry.DirectDeliveries.Select(delivery => ReadAction(delivery.Packet)).ToArray());
			Assert.Equal(1, ReadAggregateCounts(registry.DirectDeliveries[0].Packet)[0]);
			var broadcast = Assert.Single(registry.BroadcastDeliveries);
			Assert.Equal(101, broadcast.Player.ObjectId);
			Assert.Equal(0, ReadAction(broadcast.Packet));
			Assert.All(ReadAggregateCounts(broadcast.Packet), count => Assert.Equal(0, count));
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task SendRiftDespawnAsync_BroadcastsDespawnPacketOnlyToTargetWorld()
	{
		var registry = new RecordingConnectionRegistry();
		registry.Players.Add(CreatePlayer(100, 210020000));
		registry.Players.Add(CreatePlayer(101, 220020000));
		var informer = new RiftInformerService(CreateEmptyRiftService(), registry);

		var sent = await informer.SendRiftDespawnAsync(210020000, 123456);

		Assert.Equal(1, sent);
		var delivery = Assert.Single(registry.BroadcastDeliveries);
		Assert.Equal(100, delivery.Player.ObjectId);
		Assert.Equal((Action: 4, ObjectId: 123456), ReadDespawnPayload(delivery.Packet));
	}

	private static async Task<(RiftService Service, RiftInformerService Informer)> CreateServicesAsync(
		string tempPath,
		string riftLocations,
		string spawnMaps,
		IGameClientConnectionRegistry? registry = null,
		Func<DateTimeOffset>? serviceClock = null,
		Func<DateTimeOffset>? informerClock = null)
	{
		var context = await CreateRuntimeContextAsync(tempPath, riftLocations, spawnMaps);
		var idFactory = new IDFactory();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var manager = new RiftManagerService(context, world, idFactory);
		var service = new RiftService(context, manager, world, idFactory, nowProvider: serviceClock);
		return (service, new RiftInformerService(service, registry, informerClock));
	}

	private static RiftService CreateEmptyRiftService()
	{
		var context = new GameServerRuntimeContext();
		var idFactory = new IDFactory();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var manager = new RiftManagerService(context, world, idFactory);
		return new RiftService(context, manager, world, idFactory);
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextAsync(
		string tempPath,
		string riftLocations,
		string spawnMaps)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			$$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<rift_locations>
			{{riftLocations}}
				</rift_locations>
				<npc_templates>
					<npc_template npc_id="730100" name="master rift" name_id="730100" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="5" ai="portal" />
					<npc_template npc_id="730101" name="slave rift" name_id="730101" level="1" rank="NORMAL" rating="NORMAL" race="ASMODIANS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="6" ai="portal" />
				</npc_templates>
				<spawns>
			{{spawnMaps}}
				</spawns>
			</static_data>
			""");
		File.WriteAllText(schemaFile, """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
		var dataManager = await DataManager.LoadAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = staticDataFile,
				CacheXmlFilePath = cacheFile,
				SchemaFilePath = schemaFile,
				ValidateWhenCacheChanges = false,
			});
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		return context;
	}

	private static Player CreatePlayer(int objectId, int worldId)
	{
		return new Player
		{
			ObjectId = objectId,
			Position = new WorldPosition(worldId, 0, 0, 0, 0),
		};
	}

	private static int[] ReadAggregateCounts(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(49, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		var counts = new int[12];
		for (var i = 0; i < counts.Length; i++)
			counts[i] = reader.ReadD();
		Assert.Equal(0, reader.Remaining);
		return counts;
	}

	private static (int Action, int ObjectId) ReadDespawnPayload(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(5, reader.ReadH());
		var action = reader.ReadC();
		var objectId = reader.ReadD();
		Assert.Equal(0, reader.Remaining);
		return ((int)action, objectId);
	}

	private static int ReadAction(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadH();
		return reader.ReadC();
	}

	private static PortalDetailPayload ReadPortalDetailPayload(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(35, reader.ReadH());
		Assert.Equal(2, (int)reader.ReadC());
		var payload = new PortalDetailPayload(
			reader.ReadD(),
			reader.ReadD(),
			reader.ReadD(),
			reader.ReadD(),
			reader.ReadD(),
			reader.ReadF(),
			reader.ReadF(),
			reader.ReadF(),
			reader.ReadC(),
			reader.ReadC());
		Assert.Equal(0, reader.Remaining);
		return payload;
	}

	private static PortalEntryUpdatePayload ReadPortalEntryUpdatePayload(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(15, reader.ReadH());
		Assert.Equal(3, (int)reader.ReadC());
		var payload = new PortalEntryUpdatePayload(
			reader.ReadD(),
			reader.ReadD(),
			reader.ReadD(),
			reader.ReadC(),
			reader.ReadC());
		Assert.Equal(0, reader.Remaining);
		return payload;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<Player> Players { get; } = [];

		public List<BroadcastDelivery> BroadcastDeliveries { get; } = [];

		public List<DirectDelivery> DirectDeliveries { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = Players.FirstOrDefault(value => string.Equals(value.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in Players)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			if (Players.All(player => player.ObjectId != playerObjectId))
				return Task.FromResult(false);

			DirectDeliveries.Add(new DirectDelivery(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			var targets = Players.Where(player => filter?.Invoke(player) ?? true).ToArray();
			foreach (var player in targets)
				BroadcastDeliveries.Add(new BroadcastDelivery(player, packet));
			return Task.FromResult(targets.Length);
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

	private sealed record BroadcastDelivery(Player Player, GameServerPacket Packet);

	private sealed record DirectDelivery(int PlayerObjectId, GameServerPacket Packet);

	private sealed record PortalDetailPayload(
		int ObjectId,
		int MaxEntries,
		int RemainTime,
		int MinLevel,
		int MaxLevel,
		float X,
		float Y,
		float Z,
		int RiftType,
		int Display);

	private sealed record PortalEntryUpdatePayload(
		int ObjectId,
		int UsedEntries,
		int RemainTime,
		int RiftType,
		int Unknown);
}
