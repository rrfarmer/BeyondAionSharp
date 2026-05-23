using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskLifetimeServiceTests
{
	[Fact]
	public void DespawnExpiredKiskRemovesRegistryWorldObjectAndReleasesId()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var idFactory = new IDFactory([1]);
		var kiskState = new PlayerKiskRuntimeState(
			objectId: 1,
			ownerObjectId: 1001,
			npcId: 700273);
		Assert.True(kiskState.AddMember(1001));
		Assert.True(kiskState.AddMember(1002));
		registry.RegisterKisk(kiskState);
		Assert.True(world.TryAddObject(1, CreateKiskNpc(1, 700273)));

		var result = PlayerKiskLifetimeService.DespawnExpiredKisk(world, registry, idFactory, 1);

		Assert.True(result.RemovedRegistry);
		Assert.True(result.RemovedWorldObject);
		Assert.True(result.ReleasedObjectId);
		Assert.Equal(210010000, result.WorldId);
		Assert.Equal([1001, 1002], result.MemberObjectIds.Order().ToArray());
		Assert.False(registry.HaveKisk(1001));
		Assert.False(world.TryGetObject(1, out _));
		Assert.Equal(1, idFactory.NextId());
	}

	[Fact]
	public void DespawnExpiredKiskDoesNotRemoveWorldObjectWhenRegistryEntryIsMissing()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var idFactory = new IDFactory([1]);
		Assert.True(world.TryAddObject(1, CreateKiskNpc(1, 700273)));

		var result = PlayerKiskLifetimeService.DespawnExpiredKisk(world, registry, idFactory, 1);

		Assert.False(result.RemovedRegistry);
		Assert.False(result.RemovedWorldObject);
		Assert.False(result.ReleasedObjectId);
		Assert.True(world.TryGetObject(1, out _));
		Assert.NotEqual(1, idFactory.NextId());
	}

	private static WorldNpc CreateKiskNpc(int objectId, int npcId)
	{
		var template = new NpcTemplateSummary(
			npcId,
			"test_kisk",
			NameId: npcId + 100,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "PC_LIGHT_CASTLE_DOOR",
			Tribe: "KISK",
			Type: "NPC",
			MaxHp: 1000,
			Height: 2.5f,
			BoundRadius: 1.2f,
			State: WorldNpcState.DefaultSpawnState);
		return new WorldNpc(
			objectId,
			npcId,
			template,
			new WorldPosition(210010000, 1, 2, 3, 4));
	}
}
