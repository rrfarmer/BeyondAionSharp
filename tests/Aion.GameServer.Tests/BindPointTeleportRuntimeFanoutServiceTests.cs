using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRuntimeFanoutServiceTests
{
	[Fact]
	public async Task BroadcastControlPlanAsync_NoopsWhenControlBridgeHasNoPacket()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var controlBridge = new BindPointTeleportRuntimeControlBridgeService(owner);
		var registry = new CapturingConnectionRegistry(sentCount: 0);
		var fanout = new BindPointTeleportRuntimeFanoutService(registry);

		var controlPlan = controlBridge.CreateCancelPlan(playerObjectId: 8201, locId: 6301);
		var result = await fanout.BroadcastControlPlanAsync(
			controlPlan,
			new WorldPosition(210010000, 10, 20, 30, 0));

		Assert.Equal(BindPointTeleportRuntimeFanoutStatus.NoPacket, result.Status);
		Assert.False(result.SentPacket);
		Assert.Null(result.FanoutPlan);
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task BroadcastControlPlanAsync_CancelPlanUsesSourceIncludedVisibleFanout()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var controlBridge = new BindPointTeleportRuntimeControlBridgeService(owner);
		var registry = new CapturingConnectionRegistry(sentCount: 3);
		var fanout = new BindPointTeleportRuntimeFanoutService(registry);
		owner.ScheduleSkillUseTask(
			playerObjectId: 8202,
			locId: 6302,
			_ => ValueTask.CompletedTask,
			delay: TimeSpan.FromSeconds(30));
		var sourcePosition = new WorldPosition(210010000, 11, 22, 33, 1);

		var controlPlan = controlBridge.CreateCancelPlan(playerObjectId: 8202, locId: 6302);
		var result = await fanout.BroadcastControlPlanAsync(controlPlan, sourcePosition);

		Assert.Equal(BindPointTeleportRuntimeFanoutStatus.BroadcastVisiblePlayersAndSelf, result.Status);
		Assert.True(result.SentPacket);
		Assert.Equal(3, result.SentCount);
		Assert.NotNull(result.FanoutPlan);
		Assert.Equal(BindPointTeleportFanoutSource.CancelBroadcast, result.FanoutPlan.Source);
		Assert.True(result.FanoutPlan.IncludeSourcePlayer);
		Assert.Single(registry.Broadcasts);
		Assert.Equal(sourcePosition, registry.Broadcasts[0].SourcePosition);
		Assert.Equal(8202, registry.Broadcasts[0].SourceObjectId);
		Assert.True(registry.Broadcasts[0].IncludeSourcePlayer);

		var packet = Assert.IsType<SmBindPointTeleport>(registry.Broadcasts[0].Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(8202, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public async Task BroadcastControlPlanAsync_LoginCooldownUsesBroadcastPacketAndReceiveSemantics()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var controlBridge = new BindPointTeleportRuntimeControlBridgeService(owner);
		var registry = new CapturingConnectionRegistry(sentCount: 2);
		var fanout = new BindPointTeleportRuntimeFanoutService(registry);
		owner.AddCooldown(playerObjectId: 8203, locId: 6303, currentTimeMillis: 1_000);
		var sourcePosition = new WorldPosition(210010000, 12, 23, 34, 2);

		var controlPlan = controlBridge.CreateLoginCooldownPlan(
			playerObjectId: 8203,
			currentTimeMillis: 2_499);
		var result = await fanout.BroadcastControlPlanAsync(controlPlan, sourcePosition);

		Assert.Equal(BindPointTeleportRuntimeFanoutStatus.BroadcastVisiblePlayersAndSelf, result.Status);
		Assert.True(result.SentPacket);
		Assert.Equal(2, result.SentCount);
		Assert.NotNull(result.FanoutPlan);
		Assert.Equal(BindPointTeleportFanoutSource.LoginCooldownBroadcast, result.FanoutPlan.Source);
		Assert.Equal("PacketSendUtility.broadcastPacketAndReceive(player, packet)", result.FanoutPlan.JavaUtilityMethod);
		Assert.True(result.FanoutPlan.IncludeSourcePlayer);
		Assert.Single(registry.Broadcasts);
		Assert.Equal(sourcePosition, registry.Broadcasts[0].SourcePosition);
		Assert.Equal(8203, registry.Broadcasts[0].SourceObjectId);
		Assert.True(registry.Broadcasts[0].IncludeSourcePlayer);

		var packet = Assert.IsType<SmBindPointTeleport>(registry.Broadcasts[0].Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(8203, reader.ReadD());
		Assert.Equal(6303, reader.ReadD());
		Assert.Equal(598, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static ThreadPoolManager CreateThreadPoolManager()
	{
		return new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly int _sentCount;

		public CapturingConnectionRegistry(int sentCount)
		{
			_sentCount = sentCount;
		}

		public List<BroadcastRecord> Broadcasts { get; } = [];

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
			return Task.FromResult(false);
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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(_sentCount);
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

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
