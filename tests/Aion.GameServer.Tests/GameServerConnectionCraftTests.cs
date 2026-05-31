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

public sealed class GameServerConnectionCraftTests
{
	[Fact]
	public async Task ProcessPacketAsync_CmCraftWithoutActivePlayerRecordsJavaSilentNoPlayerPlan()
	{
		await using var fixture = await CraftFixture.CreateAsync();
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(unknownByte: 1, targetTemplateId: 730190, recipeId: 155000001, targetObjectId: 9001, craftType: 0));

		var plan = Assert.Single(fixture.CraftPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.NoPlayerOrNotSpawned, plan.Status);
		Assert.Null(plan.StartIntent);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmCraftShutdownSoonReturnsBeforeTargetValidation()
	{
		await using var fixture = await CraftFixture.CreateAsync(isShuttingDownSoon: () => true);
		var player = CreatePlayer(isOnline: true);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(unknownByte: 1, targetTemplateId: 730190, recipeId: 155000001, targetObjectId: 9001, craftType: 0));

		var plan = Assert.Single(fixture.CraftPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.ShuttingDownSoon, plan.Status);
		Assert.False(plan.RequiresStaticTargetValidation);
		Assert.Null(plan.StartIntent);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmCraftNonMorphInvalidTargetRecordsSilentInvalidPlan()
	{
		await using var fixture = await CraftFixture.CreateAsync();
		var player = CreatePlayer(isOnline: true);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateCraftTarget(objectId: 9001, templateId: 730190, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(unknownByte: 1, targetTemplateId: 730190, recipeId: 155000001, targetObjectId: 9001, craftType: 0));

		var plan = Assert.Single(fixture.CraftPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.InvalidNonMorphTarget, plan.Status);
		Assert.True(plan.RequiresStaticTargetValidation);
		Assert.Null(plan.StartIntent);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmCraftMorphBypassesMissingTargetAndRecordsStartIntent()
	{
		await using var fixture = await CraftFixture.CreateAsync();
		var player = CreatePlayer(isOnline: true);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(
				unknownByte: CmCraftRuntimePlanService.MorphSubstancesMarker,
				targetTemplateId: 0,
				recipeId: 155000078,
				targetObjectId: 0,
				craftType: 1,
				new Dictionary<int, long> { [186000040] = 3, [186000041] = 7 }));

		var plan = Assert.Single(fixture.CraftPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.StartCrafting, plan.Status);
		Assert.NotNull(plan.StartIntent);
		Assert.False(plan.RequiresStaticTargetValidation);
		Assert.True(plan.StartIntent!.UsesMorphTargetBypass);
		Assert.Equal(155000078, plan.StartIntent.RecipeId);
		Assert.Equal(1, plan.StartIntent.CraftType);
		Assert.Equal(2, plan.StartIntent.MaterialsData.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmCraftStartIntentRecordsNonLiveCompositionPlan()
	{
		await using var fixture = await CraftFixture.CreateAsync();
		var player = CreatePlayer(isOnline: true);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(
				unknownByte: CmCraftRuntimePlanService.MorphSubstancesMarker,
				targetTemplateId: 0,
				recipeId: 155000078,
				targetObjectId: 0,
				craftType: 1,
				new Dictionary<int, long> { [186000040] = 3 }));

		var runtimePlan = Assert.Single(fixture.CraftPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.StartCrafting, runtimePlan.Status);
		var compositionPlan = Assert.Single(fixture.CraftStartCompositionPlans);
		Assert.Equal(CmCraftStartCompositionPlanStatus.ValidationFailed, compositionPlan.Status);
		Assert.Same(runtimePlan, compositionPlan.RuntimePlan);
		Assert.Equal(CraftStartValidationStatus.MissingRecipe, compositionPlan.ValidationPlan?.Status);
		Assert.Equal(1, compositionPlan.RuntimePlan!.StartIntent?.CraftType);
		Assert.Equal(3, compositionPlan.RuntimePlan.StartIntent!.MaterialsData[186000040]);
		Assert.False(compositionPlan.IsLive);
		Assert.False(compositionPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmCraftRuntimeBlockedRecordsCompositionWithoutPlannerSideEffects()
	{
		await using var fixture = await CraftFixture.CreateAsync();
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(unknownByte: 1, targetTemplateId: 730190, recipeId: 155000001, targetObjectId: 9001, craftType: 0));

		var runtimePlan = Assert.Single(fixture.CraftPlans);
		var compositionPlan = Assert.Single(fixture.CraftStartCompositionPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.NoPlayerOrNotSpawned, runtimePlan.Status);
		Assert.Equal(CmCraftStartCompositionPlanStatus.RuntimeBlocked, compositionPlan.Status);
		Assert.Same(runtimePlan, compositionPlan.RuntimePlan);
		Assert.Null(compositionPlan.ValidationPlan);
		Assert.Null(compositionPlan.ConsumptionPlan);
		Assert.Null(compositionPlan.TaskPlan);
		Assert.False(compositionPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmCraftWithStaticDataRecordsReadyCompositionPlan()
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false));
		await using var fixture = await CraftFixture.CreateAsync(runtimeContext: runtimeContext);
		var player = CreatePlayer(isOnline: true);
		player.Dp = 500;
		player.Recipes = [155000001];
		player.Skills = [new PlayerSkill { SkillId = CraftStartValidationPlan.MorphSubstancesSkillId, SkillLevel = 10 }];
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 2001, ItemId = 152000901, Count = 1, Location = 0 },
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateCraftPayload(
				unknownByte: CmCraftRuntimePlanService.MorphSubstancesMarker,
				targetTemplateId: 0,
				recipeId: 155000001,
				targetObjectId: 0,
				craftType: 0,
				new Dictionary<int, long> { [152000901] = 1 }));

		var runtimePlan = Assert.Single(fixture.CraftPlans);
		Assert.Equal(CmCraftRuntimePlanStatus.StartCrafting, runtimePlan.Status);
		var compositionPlan = Assert.Single(fixture.CraftStartCompositionPlans);
		Assert.Equal(CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart, compositionPlan.Status);
		Assert.True(compositionPlan.ValidationPlan?.IsReadyForNextValidation);
		Assert.Equal(CraftStartConsumptionStatus.Planned, compositionPlan.ConsumptionPlan?.Status);
		var decrease = Assert.Single(compositionPlan.ConsumptionPlan!.Decreases);
		Assert.Equal(152000901, decrease.ItemId);
		Assert.Equal(1, decrease.Quantity);
		Assert.Equal(CraftStartInventoryMutationStatus.Planned, compositionPlan.InventoryMutationPlan?.Status);
		Assert.Equal([2001], compositionPlan.InventoryMutationPlan!.DeletedObjectIds);
		Assert.Empty(compositionPlan.InventoryMutationPlan.UpdatedItems);
		Assert.Equal(CraftStartInventoryPacketStatus.Planned, compositionPlan.InventoryPacketPlan?.Status);
		Assert.Collection(
			compositionPlan.InventoryPacketPlan!.Packets,
			packet => Assert.IsType<SmDeleteItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet));
		Assert.Equal(CraftStartTaskPlanStatus.Planned, compositionPlan.TaskPlan?.Status);
		Assert.Equal(200, compositionPlan.TaskPlan!.Interval);
		Assert.True(compositionPlan.RequiresDpSpend);
		Assert.Equal(200, compositionPlan.RequiredDp);
		Assert.False(compositionPlan.IsLive);
		Assert.False(compositionPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer(bool isOnline) =>
		new()
		{
			ObjectId = 1001,
			Name = "CraftTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			IsOnline = isOnline,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};

	private static WorldNpc CreateCraftTarget(int objectId, int templateId, WorldPosition position)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Crafting Station",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "STATIC");
		return new WorldNpc(objectId, templateId, template, position);
	}

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

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

	private static byte[] CreateCraftPayload(
		int unknownByte,
		int targetTemplateId,
		int recipeId,
		int targetObjectId,
		int craftType,
		IReadOnlyDictionary<int, long>? materials = null)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(141);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteC(unknownByte);
		buffer.WriteD(targetTemplateId);
		buffer.WriteD(recipeId);
		buffer.WriteD(targetObjectId);
		buffer.WriteH(materials?.Count ?? 0);
		buffer.WriteC(craftType);
		if (materials != null)
		{
			foreach (var (itemId, count) in materials)
			{
				buffer.WriteD(itemId);
				buffer.WriteQ(count);
			}
		}

		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private sealed class CraftFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private CraftFixture(
			TcpClient client,
			GameServerConnection connection,
			GameWorld world,
			List<CmCraftRuntimePlan> craftPlans,
			List<CmCraftStartCompositionPlan> craftStartCompositionPlans,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			World = world;
			CraftPlans = craftPlans;
			CraftStartCompositionPlans = craftStartCompositionPlans;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public GameWorld World { get; }

		public List<CmCraftRuntimePlan> CraftPlans { get; }

		public List<CmCraftStartCompositionPlan> CraftStartCompositionPlans { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<CraftFixture> CreateAsync(
			Func<bool>? isShuttingDownSoon = null,
			GameServerRuntimeContext? runtimeContext = null)
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
				var craftPlans = new List<CmCraftRuntimePlan>();
				var craftStartCompositionPlans = new List<CmCraftStartCompositionPlan>();
				var sentPackets = new List<GameServerPacket>();
				var fixture = new CraftFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"cm-craft-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						options: new GameServerOptions(),
						runtimeContext: runtimeContext,
						world: world,
						crypt: crypt,
						sentPacketObserver: sentPackets.Add,
						isShuttingDownSoon: isShuttingDownSoon,
						cmCraftRuntimePlanObserver: craftPlans.Add,
						cmCraftStartCompositionPlanObserver: craftStartCompositionPlans.Add),
					world,
					craftPlans,
					craftStartCompositionPlans,
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

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "game-server")))
			directory = directory.Parent;

		Assert.NotNull(directory);
		return directory.FullName;
	}
}
