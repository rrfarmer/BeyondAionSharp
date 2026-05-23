using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PortalEntryValidationServiceTests
{
	[Fact]
	public void ValidateCooldown_AllowsWhenJavaCooldownCountIsBelowMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 1);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Single(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldown_RejectsWithJavaSystemMessageWhenCountMeetsMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidateCooldown_RemovesExpiredJavaCooldownAndAllowsEntry()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 99_999, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Empty(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_SkipsCooldownLockoutForSameSoloInstance()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId: 2);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.False(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_MarksReenterWhenRegisteredElsewhere()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.True(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_RejectsUnregisteredSoloEntryWhenCooldownLocked()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		Assert.Null(result.RegisteredInstance);
		Assert.False(result.Reenter);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	private const int WorldId = 300030000;

	private static Player CreatePlayerWithCooldown(long reuseTimeMillis, int entryCount)
	{
		return new Player
		{
			ObjectId = 1001,
			PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
			{
				[WorldId] = new(WorldId, reuseTimeMillis, entryCount),
			},
		};
	}

	private static InstanceCooltimeTable CreateCooltimes(int maxCount)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, WorldId, "PC_ALL", maxCount),
		]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMapsWithRegisteredSoloInstance(
		int playerObjectId,
		out WorldMapInstanceRuntimeState instance)
	{
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
		instance = worldMaps.AddWorldMapInstance(WorldId, instanceId: 2, ownerId: 0, maxPlayers: 1)!;
		instance.Register(playerObjectId);
		return worldMaps;
	}
}
