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

public sealed class RiftPortalInteractionServiceTests
{
	[Fact]
	public async Task RequestDialog_ForShowDialogTarget_SetsPendingPortalQuestion()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-request-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (riftService, interaction) = await CreateServicesAsync(tempPath);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var player = CreatePlayer(level: 30);

			var result = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);

			Assert.True(result.Requested);
			Assert.Equal(RiftPortalDialogStatus.Requested, result.Status);
			Assert.Equal(SmQuestionWindow.DirectPortalPassConfirm, result.QuestionWindow?.Code);
			Assert.Equal(new PendingRiftPortalRequest(portal.MasterNpc.ObjectId, SmQuestionWindow.DirectPortalPassConfirm), player.PendingRiftPortalRequest);
			Assert.Equal(1, player.ResponseRequester.Count);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RequestDialog_DuplicatePortalQuestionIsRejectedThroughResponseRequester()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-duplicate-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (riftService, interaction) = await CreateServicesAsync(tempPath);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var player = CreatePlayer(level: 30);
			var first = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);

			var duplicate = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);

			Assert.True(first.Requested);
			Assert.False(duplicate.Requested);
			Assert.Equal(RiftPortalDialogStatus.PendingRequest, duplicate.Status);
			Assert.Null(duplicate.QuestionWindow);
			Assert.Equal(new PendingRiftPortalRequest(portal.MasterNpc.ObjectId, SmQuestionWindow.DirectPortalPassConfirm), player.PendingRiftPortalRequest);
			Assert.Equal(1, player.ResponseRequester.Count);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RespondAsync_ForAcceptedPortalQuestion_TeleportsAndRefreshesEntryUpdates()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-accept-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var registry = new RecordingConnectionRegistry();
			registry.Players.Add(CreatePlayer(level: 30, objectId: 100, worldId: 210020000));
			registry.Players.Add(CreatePlayer(level: 30, objectId: 101, worldId: 220020000));
			var now = DateTimeOffset.FromUnixTimeSeconds(1000);
			var (riftService, interaction) = await CreateServicesAsync(tempPath, registry, () => now);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var player = registry.Players[0];
			Assert.True(interaction.RequestDialog(player, portal.MasterNpc.ObjectId).Requested);

			var result = await interaction.RespondAsync(player, SmQuestionWindow.DirectPortalPassConfirm, response: 1);

			Assert.True(result.Handled);
			Assert.True(result.Accepted);
			Assert.Equal(RiftPortalQuestionResponseStatus.Accepted, result.Status);
			Assert.Equal(portal.SlaveNpc.SpawnLocation, player.Position);
			Assert.Null(player.PendingRiftPortalRequest);
			Assert.Equal(0, player.ResponseRequester.Count);
			Assert.Equal(1, portal.UsedEntries);
			Assert.Equal(PlayerTeamMembership.None, result.RemovedTeamMembership);
			Assert.False(result.VortexOpenNoticeRequested);
			Assert.False(result.VortexOpenNoticeSent);
			Assert.Equal(2, result.RefreshPacketsSent);
			Assert.Equal([100, 101], registry.BroadcastDeliveries.Select(delivery => delivery.Player.ObjectId).ToArray());
			Assert.Equal([3, 3], registry.BroadcastDeliveries.Select(delivery => ReadAction(delivery.Packet)).ToArray());
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RequestDialog_IgnoresPortalMasterNoLongerVisibleInWorld()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-stale-master-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (world, riftService, interaction) = await CreateServicesWithWorldAsync(tempPath);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			Assert.True(world.TryRemoveObject(portal.MasterNpc.ObjectId, out _));
			var player = CreatePlayer(level: 30);

			var result = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);

			Assert.False(result.Requested);
			Assert.Equal(RiftPortalDialogStatus.UnknownPortal, result.Status);
			Assert.Null(player.PendingRiftPortalRequest);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RequestDialog_IgnoresPortalMasterOutsidePlayerKnownList()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-known-list-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (_, riftService, interaction) = await CreateServicesWithWorldAsync(
				tempPath,
				isKnownNpc: (_, _) => false);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var player = CreatePlayer(level: 30);

			var result = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);

			Assert.False(result.Requested);
			Assert.Equal(RiftPortalDialogStatus.UnknownPortal, result.Status);
			Assert.Null(player.PendingRiftPortalRequest);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RespondAsync_ForAcceptedVortexQuestion_UsesVortexLocationStartPoint()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-vortex-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var registry = new RecordingConnectionRegistry();
			var player = CreatePlayer(level: 50, objectId: 100, worldId: 110070000);
			player.TeamMembership = PlayerTeamMembership.Group;
			registry.Players.Add(player);
			registry.Players.Add(CreatePlayer(level: 50, objectId: 101, worldId: 120080000));
			var responderPackets = new List<GameServerPacket>();
			var noticeBeforeRefresh = false;
			var now = DateTimeOffset.FromUnixTimeSeconds(1000);
			var (context, riftService, interaction) = await CreateVortexServicesAsync(tempPath, registry, () => now);
			Assert.True(riftService.OpenRifts(1170, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);

			var request = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);
			Assert.True(request.Requested);
			Assert.Equal(SmQuestionWindow.VortexPortalPassConfirm, request.QuestionWindow?.Code);
			var expectedDestination = context.DataManager?.StaticData.VortexLocations.GetLocation(1)?.StartPoint;

			var result = await interaction.RespondAsync(
				player,
				SmQuestionWindow.VortexPortalPassConfirm,
				response: 1,
				packet =>
				{
					noticeBeforeRefresh = registry.BroadcastDeliveries.Count == 0;
					responderPackets.Add(packet);
					return Task.CompletedTask;
				});

			Assert.True(result.Handled);
			Assert.True(result.Accepted);
			Assert.Equal(RiftPortalQuestionResponseStatus.Accepted, result.Status);
			Assert.Equal(expectedDestination, player.Position);
			Assert.Null(player.PendingRiftPortalRequest);
			Assert.Equal(0, player.ResponseRequester.Count);
			Assert.Equal(PlayerTeamMembership.None, player.TeamMembership);
			Assert.Equal(PlayerTeamMembership.Group, result.RemovedTeamMembership);
			Assert.True(result.VortexOpenNoticeRequested);
			Assert.True(result.VortexOpenNoticeSent);
			Assert.True(noticeBeforeRefresh);
			var responderPacket = Assert.IsType<SmSystemMessage>(Assert.Single(responderPackets));
			Assert.Equal(1401454, ReadSystemMessageId(responderPacket));
			Assert.Equal(1, portal.PassedPlayerCount);
			Assert.Equal(1, portal.UsedEntries);
			Assert.Equal(1, result.RefreshPacketsSent);
			var delivery = Assert.Single(registry.BroadcastDeliveries);
			Assert.Equal(101, delivery.Player.ObjectId);
			Assert.Equal(3, ReadAction(delivery.Packet));
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RespondAsync_ForDeclinedPortalQuestion_ClearsPendingWithoutTeleport()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-decline-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var registry = new RecordingConnectionRegistry();
			var (riftService, interaction) = await CreateServicesAsync(tempPath, registry);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var player = CreatePlayer(level: 30);
			var originalPosition = player.Position;
			Assert.True(interaction.RequestDialog(player, portal.MasterNpc.ObjectId).Requested);

			var result = await interaction.RespondAsync(player, SmQuestionWindow.DirectPortalPassConfirm, response: 0);

			Assert.True(result.Handled);
			Assert.False(result.Accepted);
			Assert.Equal(RiftPortalQuestionResponseStatus.Declined, result.Status);
			Assert.Null(player.PendingRiftPortalRequest);
			Assert.Equal(0, player.ResponseRequester.Count);
			Assert.Equal(originalPosition, player.Position);
			Assert.Empty(registry.BroadcastDeliveries);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	[Fact]
	public async Task RespondAsync_WrongQuestionLeavesPortalRequestRegistered()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-interaction-wrong-question-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var (riftService, interaction) = await CreateServicesAsync(tempPath);
			Assert.True(riftService.OpenRifts(2120, guards: false).Succeeded);
			var portal = Assert.Single(riftService.GetActiveRifts()).Portal;
			Assert.NotNull(portal);
			var player = CreatePlayer(level: 30);
			var request = interaction.RequestDialog(player, portal.MasterNpc.ObjectId);
			Assert.True(request.Requested);

			var result = await interaction.RespondAsync(player, SmQuestionWindow.BuddyListAddBuddyRequest, response: 1);

			Assert.False(result.Handled);
			Assert.Equal(RiftPortalQuestionResponseStatus.NoPendingRequest, result.Status);
			Assert.Equal(new PendingRiftPortalRequest(portal.MasterNpc.ObjectId, SmQuestionWindow.DirectPortalPassConfirm), player.PendingRiftPortalRequest);
			Assert.Equal(1, player.ResponseRequester.Count);
		}
		finally
		{
			DeleteTempDirectory(tempPath);
		}
	}

	private static async Task<(RiftService RiftService, RiftPortalInteractionService Interaction)> CreateServicesAsync(
		string tempPath,
		IGameClientConnectionRegistry? registry = null,
		Func<DateTimeOffset>? clock = null)
	{
		var (_, riftService, interaction) = await CreateServicesWithWorldAsync(tempPath, registry, clock);
		return (riftService, interaction);
	}

	private static async Task<(GameWorld World, RiftService RiftService, RiftPortalInteractionService Interaction)> CreateServicesWithWorldAsync(
		string tempPath,
		IGameClientConnectionRegistry? registry = null,
		Func<DateTimeOffset>? clock = null,
		Func<Player, int, bool>? isKnownNpc = null)
	{
		var context = await CreateRuntimeContextAsync(tempPath);
		var idFactory = new IDFactory();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var manager = new RiftManagerService(context, world, idFactory);
		var riftService = new RiftService(context, manager, world, idFactory, nowProvider: clock);
		var informer = new RiftInformerService(riftService, registry, clock);
		var interaction = new RiftPortalInteractionService(
			riftService,
			new RiftPortalDialogService(),
			new RiftPortalUseService(),
			informer,
			world: world,
			isKnownNpc: isKnownNpc);
		return (world, riftService, interaction);
	}

	private static async Task<(GameServerRuntimeContext Context, RiftService RiftService, RiftPortalInteractionService Interaction)> CreateVortexServicesAsync(
		string tempPath,
		IGameClientConnectionRegistry? registry = null,
		Func<DateTimeOffset>? clock = null)
	{
		var context = await CreateVortexRuntimeContextAsync(tempPath);
		var idFactory = new IDFactory();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var manager = new RiftManagerService(context, world, idFactory);
		var riftService = new RiftService(context, manager, world, idFactory, nowProvider: clock);
		var informer = new RiftInformerService(riftService, registry, clock);
		var interaction = new RiftPortalInteractionService(
			riftService,
			new RiftPortalDialogService(),
			new RiftPortalUseService(),
			informer,
			new VortexLocationService(context),
			world);
		return (context, riftService, interaction);
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextAsync(string tempPath)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<rift_locations>
					<rift_location id="2120" world="210020000" />
				</rift_locations>
				<npc_templates>
					<npc_template npc_id="730100" name="master rift" name_id="730100" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="5" ai="portal" />
					<npc_template npc_id="730101" name="slave rift" name_id="730101" level="1" rank="NORMAL" rating="NORMAL" race="ASMODIANS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="6" ai="portal" />
				</npc_templates>
				<spawns>
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

	private static async Task<GameServerRuntimeContext> CreateVortexRuntimeContextAsync(string tempPath)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<rift_locations>
					<rift_location id="1170" world="110070000" />
				</rift_locations>
				<dimensional_vortex>
					<vortex_location id="0" defends_race="ELYOS" offence_race="ASMODIANS">
						<home_point map="120080000" x="559.4" y="207.8" z="93.5" h="0" />
						<resurrection_point map="210060000" x="951.0" y="2433.0" z="107.0" h="0" />
						<start_point map="210060000" x="951.0" y="2433.0" z="107.0" h="0" />
					</vortex_location>
					<vortex_location id="1" defends_race="ASMODIANS" offence_race="ELYOS">
						<home_point map="110070000" x="452.6" y="237.1" z="127.0" h="0" />
						<resurrection_point map="220050000" x="2237.3" y="2801.5" z="73.3" h="0" />
						<start_point map="220050000" x="2242.0" y="2797.0" z="75.4" h="0" />
					</vortex_location>
				</dimensional_vortex>
				<npc_templates>
					<npc_template npc_id="831141" name="master vortex" name_id="831141" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="5" ai="portal" />
					<npc_template npc_id="831142" name="slave vortex" name_id="831142" level="1" rank="NORMAL" rating="NORMAL" race="ASMODIANS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="6" ai="portal" />
				</npc_templates>
				<spawns>
					<spawn_map map_id="110070000">
						<rift_spawn id="1170" world="110070000">
							<spawn npc_id="831141">
								<spot x="1" y="2" z="3" anchor="KAISINEL_AM" />
							</spawn>
						</rift_spawn>
					</spawn_map>
					<spawn_map map_id="120080000">
						<rift_spawn id="1170" world="120080000">
							<spawn npc_id="831142">
								<spot x="5" y="6" z="7" anchor="KAISINEL_AS" />
							</spawn>
						</rift_spawn>
					</spawn_map>
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

	private static Player CreatePlayer(int level, int objectId = 100, int worldId = 210020000)
	{
		return new Player
		{
			ObjectId = objectId,
			Level = level,
			Race = "ELYOS",
			Position = new WorldPosition(worldId, 1, 1, 1, 0),
		};
	}

	private static int ReadAction(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadH();
		return reader.ReadC();
	}

	private static int ReadSystemMessageId(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadC();
		reader.ReadC();
		reader.ReadD();
		return reader.ReadD();
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static void DeleteTempDirectory(string tempPath)
	{
		try
		{
			Directory.Delete(tempPath, recursive: true);
		}
		catch
		{
		}
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<Player> Players { get; } = [];

		public List<BroadcastDelivery> BroadcastDeliveries { get; } = [];

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
			return Task.FromResult(Players.Any(player => player.ObjectId == playerObjectId));
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
}
