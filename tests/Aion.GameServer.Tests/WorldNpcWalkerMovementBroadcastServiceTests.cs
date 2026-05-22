using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerMovementBroadcastServiceTests
{
	[Fact]
	public async Task BroadcastWalkerMovementAsync_SendsNpcSmMoveToVisiblePlayers()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var npc = CreateNpc(100, new WorldPosition(210010000, 1, 2, 3, 9));
		Assert.True(world.TryAddObject(npc.ObjectId, npc));
		var registry = new CapturingConnectionRegistry { SentCount = 2 };
		var service = new WorldNpcWalkerMovementBroadcastService(world, registry);
		var movementState = CreateMovementState(npc.ObjectId, targetX: 11, targetY: 22, targetZ: 33);

		var result = await service.BroadcastWalkerMovementAsync(npc.ObjectId, movementState);

		Assert.Equal(new WorldNpcWalkerMovementBroadcastResult(Broadcasted: true, SentCount: 2), result);
		Assert.Equal(npc.Position, registry.SourcePosition);
		Assert.Equal(npc.ObjectId, registry.SourceObjectId);
		var packet = Assert.IsType<Aion.GameServer.Network.Aion.ServerPackets.SmMove>(registry.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(100, reader.ReadD());
		Assert.Equal(1, reader.ReadF());
		Assert.Equal(2, reader.ReadF());
		Assert.Equal(3, reader.ReadF());
		Assert.Equal(9, (int)reader.ReadC());
		Assert.Equal(0xE0, (int)reader.ReadC());
		Assert.Equal(11, reader.ReadF());
		Assert.Equal(22, reader.ReadF());
		Assert.Equal(33, reader.ReadF());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public async Task BroadcastWalkerMovementAsync_SkipsMissingNpc()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new CapturingConnectionRegistry();
		var service = new WorldNpcWalkerMovementBroadcastService(world, registry);

		var result = await service.BroadcastWalkerMovementAsync(404, CreateMovementState(404, 1, 2, 3));

		Assert.Equal(new WorldNpcWalkerMovementBroadcastResult(Broadcasted: false, SentCount: 0), result);
		Assert.Null(registry.Packet);
	}

	private static WorldNpcWalkerMovementState CreateMovementState(int objectId, float targetX, float targetY, float targetZ)
	{
		return WorldNpcWalkerMovementState.ForTarget(
			objectId,
			"route-a",
			string.Empty,
			isFormationMember: false,
			new WorldNpcWalkerRouteStepTarget(objectId, StepIndex: 1, X: targetX, Y: targetY, Z: targetZ, RestTime: 0, IsLastStep: false, ShouldStop: false),
			restDelay: TimeSpan.Zero,
			groupStep: 0,
			sagittalShift: 0,
			coronalShift: 0);
	}

	private static WorldNpc CreateNpc(int objectId, WorldPosition position)
	{
		return new WorldNpc(
			ObjectId: objectId,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				"walker-npc",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL"),
			Position: position);
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
		public int SentCount { get; init; } = 1;

		public WorldPosition? SourcePosition { get; private set; }

		public int SourceObjectId { get; private set; }

		public GameServerPacket? Packet { get; private set; }

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
			SourcePosition = sourcePosition;
			SourceObjectId = sourceObjectId;
			Packet = packet;
			return Task.FromResult(SentCount);
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
