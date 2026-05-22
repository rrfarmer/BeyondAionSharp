using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDropRegistrationServiceTests
{
	[Fact]
	public void RegisterDrop_TracksCurrentDropsRegistrationAndLootRights()
	{
		var service = new WorldNpcDropRegistrationService();
		var drop = new WorldNpcDropItem(Index: 1, ItemId: 182400001, Count: 25, PlayerObjectIds: new HashSet<int> { 1001 });

		service.RegisterDrop(5001, looterObjectId: 1001, drops: [drop]);

		Assert.True(service.HasRegisteredDrops(5001));
		Assert.Equal([drop], service.GetCurrentDrops(5001));
		Assert.True(service.TryGetRegistration(5001, out var registration));
		Assert.NotNull(registration);
		Assert.Equal(5001, registration.NpcObjectId);
		Assert.True(registration.IsAllowedToLoot(1001));
		Assert.False(registration.IsAllowedToLoot(1002));
		Assert.True(drop.CanViewDropItem(1001));
		Assert.False(drop.CanViewDropItem(1002));

		registration.StartFreeForAll();

		Assert.True(registration.IsFreeForAll);
		Assert.Empty(registration.AllowedLooters);
		Assert.True(registration.IsAllowedToLoot(1002));
	}

	[Fact]
	public void RegisterDrop_WithNoItemsKeepsRegistrationButNotDropDecay()
	{
		var service = new WorldNpcDropRegistrationService();

		service.RegisterDrop(5001, looterObjectId: 1001);

		Assert.False(service.HasRegisteredDrops(5001));
		Assert.Empty(service.GetCurrentDrops(5001));
		Assert.True(service.TryGetRegistration(5001, out var registration));
		Assert.NotNull(registration);
		Assert.True(registration.IsAllowedToLoot(1001));
	}

	[Fact]
	public void UnregisterDrop_RemovesCurrentDropsAndRegistration()
	{
		var service = new WorldNpcDropRegistrationService();
		service.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);

		var removed = service.UnregisterDrop(5001);

		Assert.True(removed);
		Assert.False(service.HasRegisteredDrops(5001));
		Assert.Empty(service.GetCurrentDrops(5001));
		Assert.False(service.TryGetRegistration(5001, out _));
		Assert.False(service.UnregisterDrop(5001));
	}
}
