using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionKiskReviveWorkflowTests
{
	[Fact]
	public async Task HandleReviveAsync_KiskReviveConsumesChargeRestoresAndTeleports()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		player.IsInResurrectionPositionState = true;
		player.ResurrectionPositionX = 1.5f;
		player.ResurrectionPositionY = 2.5f;
		player.ResurrectionPositionZ = 3.5f;
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		var kisk = fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(1, kisk.RemainingResurrects);
		Assert.Equal(kiskPosition, player.Position);
		Assert.Equal(new PlayerLifeStats(51, 63, 12), player.LifeStats);
		Assert.Equal(0, player.Dp);
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsInResurrectionPositionState);
		Assert.Equal(0, player.ResurrectionPositionX);
		Assert.Equal(0, player.ResurrectionPositionY);
		Assert.Equal(0, player.ResurrectionPositionZ);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmMotion>(packet));
	}

	[Fact]
	public async Task HandleReviveAsync_LastKiskReviveChargeRemovesKiskAfterUpdate()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		var kisk = fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 1);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(0, kisk.RemainingResurrects);
		Assert.False(fixture.RuntimeContext.Kisks.HaveKisk(kisk.OwnerObjectId));
		Assert.False(fixture.World.TryGetObject(kisk.ObjectId, out _));
		Assert.Equal(kiskPosition, player.Position);
		Assert.Equal(new PlayerLifeStats(51, 63, 12), player.LifeStats);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmMotion>(packet));
	}

	[Fact]
	public async Task HandleReviveAsync_KiskReviveHonorsNoResurrectPenaltyEffect()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		player.HasNoResurrectPenaltyEffect = true;
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		var kisk = fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(1, kisk.RemainingResurrects);
		Assert.Equal(kiskPosition, player.Position);
		Assert.Equal(new PlayerLifeStats(170, 210, 12), player.LifeStats);
		Assert.Equal(500, player.Dp);
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
	}

	[Fact]
	public async Task HandleReviveAsync_KiskReviveCanExposeDisabledCleanupPlanWithoutAggroMutation()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);
		var adapter = new PlayerReviveCleanupAdapterService();
		var aggroEntries = new[]
		{
			new PlayerAggroEntrySnapshot(2001, Damage: 80, Hate: 800),
			new PlayerAggroEntrySnapshot(2002, Damage: 20, Hate: 200),
		};

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));
		var observation = adapter.Apply(new PlayerReviveCleanupAdapterRequest(player.ObjectId, aggroEntries));

		Assert.Equal(kiskPosition, player.Position);
		Assert.Equal(PlayerReviveCleanupAdapterStatus.DisabledPlanned, observation.Status);
		Assert.False(observation.MutatedLiveAggro);
		Assert.True(observation.ExposesPlanForObservation);
		Assert.Equal(player.ObjectId, observation.Plan.PlayerObjectId);
		Assert.Equal(aggroEntries, observation.Plan.AggroClearPlan.ClearedEntries);
		Assert.Contains(PlayerReviveCleanupPlanStep.ClearPlayerAggro, observation.Plan.Steps);
		Assert.False(observation.IsLive);
	}

	[Fact]
	public async Task HandleReviveAsync_KiskReviveClearsLivePlayerAggro()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		player.AggroList.TryAddKnownAttacker(2001, damage: 80, hate: 800, ownerKnownListKnowsAttacker: true);
		player.AggroList.TryAddKnownAttacker(2002, damage: 20, hate: 200, ownerKnownListKnowsAttacker: true);
		player.AggroList.MarkHateReductionTaskActiveForParity();
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Empty(player.AggroList.Entries);
		Assert.False(player.AggroList.HasHateReductionTask);
		Assert.Equal(kiskPosition, player.Position);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmMotion>(packet));
	}

	[Fact]
	public async Task HandleReviveAsync_KiskReviveClearsVisibleTargetsBeforeTeleport()
	{
		var registry = new CapturingConnectionRegistry();
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync(registry);
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		player.Position = new WorldPosition(210010000, 10, 10, 20, 0);
		var visibleTargeter = CreateOnlinePlayer(objectId: 1003, boundKiskObjectId: 0);
		visibleTargeter.Position = player.Position with { X = player.Position.X + 5 };
		visibleTargeter.TargetObjectId = player.ObjectId;
		var distantTargeter = CreateOnlinePlayer(objectId: 1004, boundKiskObjectId: 0);
		distantTargeter.Position = player.Position with { X = player.Position.X + 300 };
		distantTargeter.TargetObjectId = player.ObjectId;
		var unrelatedTargeter = CreateOnlinePlayer(objectId: 1005, boundKiskObjectId: 0);
		unrelatedTargeter.Position = player.Position with { Y = player.Position.Y + 5 };
		unrelatedTargeter.TargetObjectId = 9999;
		registry.OnlinePlayers.AddRange([visibleTargeter, distantTargeter, unrelatedTargeter]);
		var kiskPosition = new WorldPosition(210010000, 100, 120, 33, 0);
		fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(0, visibleTargeter.TargetObjectId);
		Assert.Equal(player.ObjectId, distantTargeter.TargetObjectId);
		Assert.Equal(9999, unrelatedTargeter.TargetObjectId);
		Assert.Equal(kiskPosition, player.Position);
	}

	[Fact]
	public async Task HandleReviveAsync_KiskReviveBroadcastsTeleportDeleteFromPreRevivePosition()
	{
		var registry = new CapturingConnectionRegistry();
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync(registry);
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		var preRevivePosition = new WorldPosition(210010000, 10, 10, 20, 0);
		player.Position = preRevivePosition;
		var viewer = CreateOnlinePlayer(objectId: 1003, boundKiskObjectId: 0);
		viewer.Position = preRevivePosition with { X = preRevivePosition.X + 5 };
		registry.OnlinePlayers.Add(viewer);
		var kiskPosition = new WorldPosition(210010000, 100, 120, 33, 0);
		fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		var teleportDelete = Assert.Single(registry.Broadcasts, broadcast => broadcast.Packet is SmDelete && broadcast.SourceObjectId == player.ObjectId);
		Assert.Equal(preRevivePosition, teleportDelete.SourcePosition);
		Assert.False(teleportDelete.IncludeSourcePlayer);
		var reviveEmotions = registry.Broadcasts
			.Where(broadcast => broadcast.Packet is SmEmotion && broadcast.SourceObjectId == player.ObjectId)
			.ToArray();
		Assert.Equal(2, reviveEmotions.Length);
		Assert.All(reviveEmotions, broadcast =>
		{
			Assert.Equal(preRevivePosition, broadcast.SourcePosition);
			Assert.True(broadcast.IncludeSourcePlayer);
		});
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmMotion>(packet));
	}

	[Fact]
	public async Task HandleReviveAsync_GroupKiskReviveSendsMovementUpdateBeforeReviveEmotion()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync(
			registry,
			playerGroupRuntime: groups);
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		player.Position = new WorldPosition(210010000, 10, 10, 20, 0);
		var member = CreateOnlinePlayer(objectId: 1003, boundKiskObjectId: 0);
		member.Position = player.Position with { X = player.Position.X + 5 };
		groups.CreateOrUpdateGroup(99001, [player, member]);
		registry.OnlinePlayers.Add(member);
		fixture.RegisterKisk(
			objectId: 9001,
			new WorldPosition(210010000, 100, 120, 33, 0),
			maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		var movement = Assert.Single(registry.SentPackets, send => send.Packet is SmGroupMemberInfo);
		Assert.Equal(member.ObjectId, movement.PlayerObjectId);
		var movementIndex = registry.SentPackets.FindIndex(send => send.Packet is SmGroupMemberInfo);
		var firstReviveEmotionIndex = registry.SentPackets.FindIndex(send => send.Packet is SmEmotion);
		Assert.True(movementIndex >= 0);
		Assert.True(firstReviveEmotionIndex >= 0);
		Assert.True(movementIndex < firstReviveEmotionIndex);
		Assert.DoesNotContain(registry.SentPackets, send => send.PlayerObjectId == player.ObjectId && send.Packet is SmGroupMemberInfo);
	}

	[Fact]
	public async Task HandleReviveAsync_AllianceKiskReviveSendsMovementUpdateBeforeReviveEmotion()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync(
			registry,
			playerAllianceRuntime: alliances);
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		player.Position = new WorldPosition(210010000, 10, 10, 20, 0);
		var member = CreateOnlinePlayer(objectId: 1003, boundKiskObjectId: 0);
		member.Position = player.Position with { X = player.Position.X + 5 };
		alliances.CreateAlliance(88001, player);
		alliances.AddMember(88001, member);
		registry.OnlinePlayers.Add(member);
		fixture.RegisterKisk(
			objectId: 9001,
			new WorldPosition(210010000, 100, 120, 33, 0),
			maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		var movement = Assert.Single(registry.SentPackets, send => send.Packet is SmAllianceMemberInfo);
		Assert.Equal(member.ObjectId, movement.PlayerObjectId);
		var movementIndex = registry.SentPackets.FindIndex(send => send.Packet is SmAllianceMemberInfo);
		var firstReviveEmotionIndex = registry.SentPackets.FindIndex(send => send.Packet is SmEmotion);
		Assert.True(movementIndex >= 0);
		Assert.True(firstReviveEmotionIndex >= 0);
		Assert.True(movementIndex < firstReviveEmotionIndex);
		Assert.DoesNotContain(registry.SentPackets, send => send.PlayerObjectId == player.ObjectId && send.Packet is SmAllianceMemberInfo);
	}

	[Fact]
	public async Task HandleReviveAsync_DepletedKiskRunsRegistryCleanupFanout()
	{
		var registry = new CapturingConnectionRegistry();
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync(registry);
		var revivedPlayer = CreateDeadPlayer(boundKiskObjectId: 9001);
		var creator = CreateOnlinePlayer(objectId: 1001, boundKiskObjectId: 0);
		var deadMember = CreateOnlinePlayer(objectId: 1003, boundKiskObjectId: 9001, currentHp: 0, dead: true);
		var pendingResponder = CreateOnlinePlayer(objectId: 1004, boundKiskObjectId: 0);
		var pendingRequest = new PendingKiskBindRequest(9001, SmQuestionWindow.RegisterBindstone);
		pendingResponder.PendingKiskBindRequest = pendingRequest;
		Assert.True(pendingResponder.ResponseRequester.PutRequest(
			SmQuestionWindow.RegisterBindstone,
			new QuestionResponseRequest(9001, QuestionResponseRequestKind.KiskBind, pendingRequest)));
		registry.OnlinePlayers.AddRange([creator, deadMember, pendingResponder]);
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		var kisk = fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 1);
		Assert.True(kisk.AddMember(deadMember.ObjectId));
		Assert.True(fixture.World.TryAddObject(9002, CreateKiskNpc(9002, kiskPosition with { X = 15 })));

		await fixture.Connection.HandleReviveAsync(revivedPlayer, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(0, kisk.RemainingResurrects);
		Assert.False(fixture.RuntimeContext.Kisks.HaveKisk(kisk.OwnerObjectId));
		Assert.False(fixture.World.TryGetObject(kisk.ObjectId, out _));
		Assert.Equal(0, deadMember.BoundKiskObjectId);
		Assert.Null(pendingResponder.PendingKiskBindRequest);
		Assert.Equal(0, pendingResponder.ResponseRequester.Count);
		Assert.Contains(registry.SentPackets, delivery => delivery.PlayerObjectId == creator.ObjectId && delivery.Packet is SmKiskUpdate);
		Assert.Contains(registry.SentPackets, delivery => delivery.PlayerObjectId == deadMember.ObjectId && delivery.Packet is SmBindPointInfo);
		Assert.Contains(registry.SentPackets, delivery => delivery.PlayerObjectId == deadMember.ObjectId && delivery.Packet is SmDie);
		var operations = registry.OperationOrder;
		var kiskUpdateBroadcastIndex = operations.FindIndex(entry => entry == "broadcast:9001:SmKiskUpdate");
		var creatorFinalUpdateIndex = operations.FindLastIndex(entry => entry == $"send:{creator.ObjectId}:SmKiskUpdate");
		var bindPointIndex = operations.FindIndex(entry => entry == $"send:{deadMember.ObjectId}:SmBindPointInfo");
		var deathRefreshIndex = operations.FindIndex(entry => entry == $"send:{deadMember.ObjectId}:SmDie");
		var npcRefreshIndex = operations.FindIndex(entry => entry == "refresh-npc");
		var firstReviveEmotionIndex = operations.FindIndex(entry => entry == $"broadcast:{revivedPlayer.ObjectId}:SmEmotion");
		var teleportDeleteIndex = operations.FindIndex(entry => entry == $"broadcast:{revivedPlayer.ObjectId}:SmDelete");
		Assert.True(kiskUpdateBroadcastIndex >= 0);
		Assert.True(creatorFinalUpdateIndex >= 0);
		Assert.True(bindPointIndex >= 0);
		Assert.True(deathRefreshIndex >= 0);
		Assert.True(npcRefreshIndex >= 0);
		Assert.True(firstReviveEmotionIndex >= 0);
		Assert.True(teleportDeleteIndex >= 0);
		Assert.True(kiskUpdateBroadcastIndex < creatorFinalUpdateIndex);
		Assert.True(creatorFinalUpdateIndex < bindPointIndex);
		Assert.True(bindPointIndex < deathRefreshIndex);
		Assert.True(deathRefreshIndex < npcRefreshIndex);
		Assert.True(npcRefreshIndex < firstReviveEmotionIndex);
		Assert.True(firstReviveEmotionIndex < teleportDeleteIndex);
		Assert.True(registry.RefreshNpcVisibilityCalls >= 1);
		Assert.Contains(registry.RefreshedNpcs, npc => npc.ObjectId == 9002);
	}

	[Fact]
	public async Task HandleReviveAsync_DepletedKiskReleasesObjectId()
	{
		var idFactory = new IDFactory(Enumerable.Range(1, 9001));
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync(idFactory: idFactory);
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 1);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(9001, idFactory.NextId());
	}

	private static Player CreateDeadPlayer(int boundKiskObjectId)
	{
		return new Player
		{
			ObjectId = 1002,
			Name = "KiskUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 1,
			BoundKiskObjectId = boundKiskObjectId,
			CreatureState = PlayerCreatureState.Dead,
			Dp = 500,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 12),
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static Player CreateOnlinePlayer(int objectId, int boundKiskObjectId, int currentHp = 100, bool dead = false)
	{
		var player = new Player
		{
			ObjectId = objectId,
			Name = $"KiskOnline{objectId}",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 1,
			BoundKiskObjectId = boundKiskObjectId,
			BindPoint = new PlayerBindPoint(210010000, 1, 2, 3, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: currentHp, CurrentMp: 0, CurrentFp: 0),
			Position = new WorldPosition(210010000, 4, 5, 6, 0),
		};
		if (dead)
			player.SetCreatureState(PlayerCreatureState.Dead, enabled: true);
		return player;
	}

	private static WorldNpc CreateKiskNpc(int objectId, WorldPosition position)
	{
		var template = new NpcTemplateSummary(
			700273,
			"test_kisk",
			0,
			1,
			"NORMAL",
			"NORMAL",
			"PC_ALL",
			string.Empty,
			"NPC",
			KiskStats: new KiskStatsSummary(UseMask: 4, MaxMembers: 6, MaxResurrects: 2));
		return new WorldNpc(objectId, template.TemplateId, template, position);
	}

	private static CmRevive CreateRevive(int reviveId)
	{
		using var writer = new PacketBuffer();
		writer.WriteC(reviveId);
		var packet = new CmRevive(55, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<Player> OnlinePlayers { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public List<IWorldNpcObject> RefreshedNpcs { get; } = [];

		public List<BroadcastRecord> Broadcasts { get; } = [];

		public List<string> OperationOrder { get; } = [];

		public int RefreshNpcVisibilityCalls { get; private set; }

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = OnlinePlayers.SingleOrDefault(candidate => candidate.Name == playerName);
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in OnlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			OperationOrder.Add($"send:{playerObjectId}:{packet.GetType().Name}");
			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
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
			OperationOrder.Add($"broadcast:{sourceObjectId}:{packet.GetType().Name}");
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			var recipients = OnlinePlayers.Where(player => filter?.Invoke(player) ?? true).ToArray();
			foreach (var recipient in recipients)
			{
				OperationOrder.Add($"send:{recipient.ObjectId}:{packet.GetType().Name}");
				SentPackets.Add(new PacketDelivery(recipient.ObjectId, packet));
			}
			return Task.FromResult(recipients.Length);
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
			OperationOrder.Add("refresh-npc");
			RefreshNpcVisibilityCalls++;
			RefreshedNpcs.AddRange(npcs);
			return Task.FromResult(npcs.Count);
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

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);

	private sealed class KiskReviveWorkflowFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;

		private KiskReviveWorkflowFixture(
			TcpClient client,
			GameServerConnection connection,
			GameServerRuntimeContext runtimeContext,
			GameWorld world,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			_connection = connection;
			RuntimeContext = runtimeContext;
			World = world;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection => _connection;

		public GameServerRuntimeContext RuntimeContext { get; }

		public GameWorld World { get; }

		public List<GameServerPacket> SentPackets { get; }

		public PlayerKiskRuntimeState RegisterKisk(int objectId, WorldPosition position, int maxResurrects)
		{
			var kisk = new PlayerKiskRuntimeState(
				objectId,
				ownerObjectId: 1001,
				npcId: 700273,
				maxResurrects: maxResurrects,
				spawnedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
				ownerRace: "ELYOS");
			RuntimeContext.Kisks.RegisterKisk(kisk);
			Assert.True(World.TryAddObject(objectId, CreateKiskNpc(objectId, position)));
			return kisk;
		}

		public static async Task<KiskReviveWorkflowFixture> CreateAsync(
			IGameClientConnectionRegistry? registry = null,
			IDFactory? idFactory = null,
			PlayerGroupRuntime? playerGroupRuntime = null,
			PlayerAllianceRuntime? playerAllianceRuntime = null)
		{
			var runtimeContext = new GameServerRuntimeContext();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			world.Initialize();
			var sentPackets = new List<GameServerPacket>();

			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"kisk-revive-workflow-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					connectionRegistry: registry,
					idFactory: idFactory,
					world: world,
					sentPacketObserver: sentPackets.Add,
					playerGroupRuntime: playerGroupRuntime,
					playerAllianceRuntime: playerAllianceRuntime,
					crypt: crypt);
				return new KiskReviveWorkflowFixture(client, connection, runtimeContext, world, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
