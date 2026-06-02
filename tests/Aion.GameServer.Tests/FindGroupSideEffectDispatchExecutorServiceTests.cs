using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupSideEffectDispatchExecutorServiceTests
{
	[Fact]
	public async Task ExecuteAsync_SendsDirectPacketIntentThroughConnectionRegistry()
	{
		var registry = new FakeGameClientConnectionRegistry();
		registry.OnlineDirectRecipients.Add(1001);
		var service = new FindGroupSideEffectDispatchExecutorService(registry);
		var intent = new FindGroupDirectPacketIntent(
			1001,
			SmFindGroup.RegisterInstanceGroup(
			[
				new FindGroupInstanceGroupRegistrationSnapshot(
					GroupEntryId: 1001,
					InstanceMaskId: 0x11223344,
					MemberCount: 1,
					MinMembers: 3,
					RecruiterObjectId: 1001,
					MinLevel: 65,
					MaxLevel: 65,
					LastUpdate: 123,
					RecruiterName: "Recruiter",
					Message: "Entry"),
			]),
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))");

		var plan = await service.ExecuteAsync([intent]);

		Assert.True(plan.DispatchLiveSideEffects);
		Assert.Contains("CM_FIND_GROUP live boundary remains deferred", plan.BoundaryNote, StringComparison.Ordinal);
		var direct = Assert.Single(plan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(1001, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Equal([1001], registry.DirectSends.Select(send => send.RecipientObjectId));
		Assert.Empty(plan.WorldBroadcasts);
	}

	[Fact]
	public async Task ExecuteAsync_MissingDirectRecipientRecordsUnsentResult()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var service = new FindGroupSideEffectDispatchExecutorService(registry);
		var intent = new FindGroupDirectPacketIntent(
			404,
			SmFindGroup.RemoveApplication(404),
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(...))");

		var plan = await service.ExecuteAsync([intent]);

		var direct = Assert.Single(plan.DirectPackets);
		Assert.False(direct.Sent);
		Assert.Equal(404, direct.RecipientObjectId);
		Assert.Equal([404], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteAsync_BroadcastsWorldIntentWithJavaRaceFilter()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var elyos = CreatePlayer(1001, "Elyos", "ELYOS");
		var asmo = CreatePlayer(1002, "Asmo", "ASMODIANS");
		registry.WorldPlayers.AddRange([elyos, asmo]);
		var service = new FindGroupSideEffectDispatchExecutorService(registry);
		var intent = new FindGroupWorldBroadcastIntent(
			"ELYOS",
			SmFindGroup.RemoveApplication(7001),
			"PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == application.getPlayer().getRace())");

		var plan = await service.ExecuteAsync(worldBroadcastIntents: [intent]);

		Assert.Empty(plan.DirectPackets);
		var broadcast = Assert.Single(plan.WorldBroadcasts);
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal(1, broadcast.SentCount);
		Assert.Equal("p -> p.getRace() == recorded race", broadcast.JavaFilter);
		var recorded = Assert.Single(registry.WorldBroadcasts);
		Assert.Equal([elyos.ObjectId], recorded.RecipientObjectIds);
	}

	[Fact]
	public async Task ExecuteAsync_IgnoresNullWorldBroadcastIntent()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var service = new FindGroupSideEffectDispatchExecutorService(registry);

		var plan = await service.ExecuteAsync(
			directPacketIntents: [],
			worldBroadcastIntents: [null]);

		Assert.True(plan.DispatchLiveSideEffects);
		Assert.Empty(plan.DirectPackets);
		Assert.Empty(plan.WorldBroadcasts);
		Assert.Empty(registry.WorldBroadcasts);
	}

	private static Player CreatePlayer(int objectId, string name, string race)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = "RANGER",
			Level = 65,
			Position = new WorldPosition(210010000, 11, 22, 33, 0),
		};
	}

	private sealed class FakeGameClientConnectionRegistry : IGameClientConnectionRegistry
	{
		public HashSet<int> OnlineDirectRecipients { get; } = [];
		public List<Player> WorldPlayers { get; } = [];
		public List<DirectSendRecord> DirectSends { get; } = [];
		public List<WorldBroadcastRecord> WorldBroadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = WorldPlayers.FirstOrDefault(entry => string.Equals(entry.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in WorldPlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			DirectSends.Add(new DirectSendRecord(playerObjectId, packet));
			return Task.FromResult(OnlineDirectRecipients.Contains(playerObjectId));
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			var recipients = WorldPlayers
				.Where(player => filter == null || filter(player))
				.Select(player => player.ObjectId)
				.ToArray();
			WorldBroadcasts.Add(new WorldBroadcastRecord(packet, recipients));
			return Task.FromResult(recipients.Length);
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

	private sealed record DirectSendRecord(int RecipientObjectId, GameServerPacket Packet);

	private sealed record WorldBroadcastRecord(GameServerPacket Packet, IReadOnlyList<int> RecipientObjectIds);
}
