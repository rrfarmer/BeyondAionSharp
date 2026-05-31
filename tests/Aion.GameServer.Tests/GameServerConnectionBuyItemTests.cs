using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionBuyItemTests
{
	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemWithoutActivePlayerRecordsSilentNoPlayerPlan()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedMissingPlayer, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemUnknownTargetRecordsNonLiveDiagnosticPlan()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcTargetSelectsNonLiveBuyFromShopPlanner()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		Assert.NotNull(plan.BuyFromShopPlan);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemKnownListResolverCanRejectWorldObjectTarget()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(buyItemKnownObjectResolver: (_, _, _) => false);
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.BuyFromShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerMembershipResolverRejectsUnknownPlayerTarget()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		var sellerPlayer = new Player { ObjectId = 9101, Name = "StoreSeller", Position = new WorldPosition(210010000, 10, 0, 0, 0) };
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(1001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.PrivateStorePurchasePlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStoreSelectionRecordsDisabledOutcomeDiagnostic()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		var sellerPlayer = new Player { ObjectId = 9101, Name = "StoreSeller", Position = new WorldPosition(210010000, 10, 0, 0, 0) };
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner, plan.Status);
		Assert.NotNull(plan.PrivateStoreBoughtItemsPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.NotNull(outcome.PrivateStoreFacadePlan);
		Assert.NotNull(outcome.PrivateStoreOutcomePlan);
		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.BoughtItemsPlanNotReady, outcome.PrivateStoreFacadePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer() =>
		new()
		{
			ObjectId = 1001,
			Name = "BuyItemTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			IsOnline = true,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};

	private static WorldNpc CreateNpc(int objectId, int templateId, WorldPosition position)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Trade Npc",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC");
		return new WorldNpc(objectId, templateId, template, position);
	}

	internal static Task InvokeProcessPacketAsyncForAdapterTests(GameServerConnection connection, byte[] payload) =>
		InvokeProcessPacketAsync(connection, payload);

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	internal static void SetActivePlayerForPacketDispatchForAdapterTests(GameServerConnection connection, Player player) =>
		SetActivePlayerForPacketDispatch(connection, player);

	private static void SetActivePlayerForPacketDispatch(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);
		SetConnectionState(connection, GameConnectionState.InGame);
	}

	private static void SetConnectionState(GameServerConnection connection, GameConnectionState state)
	{
		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(stateField);
		stateField.SetValue(connection, state);
	}

	internal static byte[] CreateBuyItemPayloadForAdapterTests(
		int sellerObjectId,
		int tradeActionId,
		IReadOnlyList<(int ItemObjectId, long Count)> items) =>
		CreateBuyItemPayload(sellerObjectId, tradeActionId, items);

	private static byte[] CreateBuyItemPayload(
		int sellerObjectId,
		int tradeActionId,
		IReadOnlyList<(int ItemObjectId, long Count)> items)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(51);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteD(sellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(items.Count);
		foreach (var (itemObjectId, count) in items)
		{
			buffer.WriteD(itemObjectId);
			buffer.WriteQ(count);
		}

		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	internal sealed class BuyItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private BuyItemFixture(
			TcpClient client,
			GameServerConnection connection,
			GameWorld world,
			List<CmBuyItemHandlerCompositionPlan> buyItemPlans,
			List<CmBuyItemSideEffectOutcomePlan> buyItemSideEffectOutcomePlans,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			World = world;
			BuyItemPlans = buyItemPlans;
			BuyItemSideEffectOutcomePlans = buyItemSideEffectOutcomePlans;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public GameWorld World { get; }

		public List<CmBuyItemHandlerCompositionPlan> BuyItemPlans { get; }

		public List<CmBuyItemSideEffectOutcomePlan> BuyItemSideEffectOutcomePlans { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<BuyItemFixture> CreateAsync(Func<Player, int, object?, bool?>? buyItemKnownObjectResolver = null)
		{
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
				var world = new GameWorld(NullLogger<GameWorld>.Instance);
				world.Initialize();
				var buyItemPlans = new List<CmBuyItemHandlerCompositionPlan>();
				var buyItemSideEffectOutcomePlans = new List<CmBuyItemSideEffectOutcomePlan>();
				var sentPackets = new List<GameServerPacket>();
				var fixture = new BuyItemFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"cm-buy-item-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						options: new GameServerOptions(),
						world: world,
						crypt: crypt,
						sentPacketObserver: sentPackets.Add,
						cmBuyItemHandlerCompositionPlanObserver: buyItemPlans.Add,
						cmBuyItemSideEffectOutcomePlanObserver: buyItemSideEffectOutcomePlans.Add,
						buyItemKnownObjectResolver: buyItemKnownObjectResolver),
					world,
					buyItemPlans,
					buyItemSideEffectOutcomePlans,
					sentPackets);
				return fixture;
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
