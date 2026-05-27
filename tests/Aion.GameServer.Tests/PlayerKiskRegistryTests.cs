using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskRegistryTests
{
	[Fact]
	public void RegistryMatchesJavaOwnerKiskLookupSlice()
	{
		var registry = new PlayerKiskRegistry();

		Assert.False(registry.HaveKisk(1001));
		Assert.Null(registry.GetOwnerKisk(1001));

		var first = registry.RegisterKisk(ownerObjectId: 1001, kiskObjectId: 9001, npcId: 700273);

		Assert.Equal(new PlayerKiskOwnership(9001, 1001, 700273), first);
		Assert.True(registry.HaveKisk(1001));
		Assert.Equal(first, registry.GetOwnerKisk(1001));
		Assert.Equal(first, registry.GetOwnerKiskState(1001)?.Ownership);

		var replacement = registry.RegisterKisk(ownerObjectId: 1001, kiskObjectId: 9002, npcId: 700274);

		Assert.True(registry.HaveKisk(1001));
		Assert.Equal(replacement, registry.GetOwnerKisk(1001));
		Assert.False(registry.RemoveKisk(9001));
		Assert.True(registry.RemoveKisk(9002));
		Assert.False(registry.HaveKisk(1001));
		Assert.Null(registry.GetOwnerKisk(1001));
	}

	[Fact]
	public void RegistryStoresJavaKiskRuntimeStateSlice()
	{
		var registry = new PlayerKiskRegistry();
		var spawnedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
		var state = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			useMask: 4,
			maxMembers: 6,
			maxResurrects: 18,
			spawnedAt: spawnedAt);

		var registered = registry.RegisterKisk(state);

		Assert.Same(state, registered);
		Assert.Same(state, registry.GetOwnerKiskState(1001));
		Assert.Same(state, registry.GetKiskState(9001));
		Assert.True(state.AddMember(1001));
		Assert.False(state.AddMember(1001));
		Assert.Equal(1, state.CurrentMemberCount);
		Assert.Equal([1001], state.CurrentMemberIds);
		Assert.True(state.UseResurrection());
		Assert.Equal(17, state.RemainingResurrects);
		Assert.Equal(7190, state.GetRemainingLifetimeSeconds(spawnedAt.AddSeconds(10)));
		Assert.Equal(0, state.GetRemainingLifetimeSeconds(spawnedAt.AddSeconds(7205)));
		Assert.True(registry.TryRemoveKisk(9001, out var removed));
		Assert.Same(state, removed);
		Assert.Null(registry.GetKiskState(9001));
	}

	[Fact]
	public void RuntimeContextExposesKiskRegistry()
	{
		var runtimeContext = new GameServerRuntimeContext();

		runtimeContext.Kisks.RegisterKisk(ownerObjectId: 1001, kiskObjectId: 9001, npcId: 700273);

		Assert.True(runtimeContext.Kisks.HaveKisk(1001));
	}

	[Fact]
	public void OfflineBindingRestoreMatchesJavaKiskServiceLoginLogoutSlice()
	{
		var registry = new PlayerKiskRegistry();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273);
		Assert.True(kisk.AddMember(1002));
		registry.RegisterKisk(kisk);
		Assert.True(registry.RegisterOfflineBinding(playerObjectId: 1002, kiskObjectId: 9001));
		var player = new Player { ObjectId = 1002 };

		var result = registry.RestoreOfflineBinding(player);
		var secondResult = registry.RestoreOfflineBinding(player);

		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.RestoredExistingMember, result.Status);
		Assert.Same(kisk, result.Kisk);
		Assert.False(result.AddedMember);
		Assert.Equal(9001, player.BoundKiskObjectId);
		Assert.Equal(1, kisk.CurrentMemberCount);
		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.NotFound, secondResult.Status);
	}

	[Fact]
	public void OfflineBindingRestoreAddsMissingMemberAndExpiresWhenKiskIsRemoved()
	{
		var registry = new PlayerKiskRegistry();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273);
		registry.RegisterKisk(kisk);
		Assert.False(registry.RegisterOfflineBinding(playerObjectId: 1002, kiskObjectId: 8001));
		Assert.True(registry.RegisterOfflineBinding(playerObjectId: 1002, kiskObjectId: 9001));
		var player = new Player { ObjectId = 1002 };

		var result = registry.RestoreOfflineBinding(player);

		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.RestoredAddedMember, result.Status);
		Assert.True(result.AddedMember);
		Assert.Contains(1002, kisk.CurrentMemberIds);
		Assert.Equal(9001, player.BoundKiskObjectId);

		Assert.True(registry.RegisterOfflineBinding(playerObjectId: 1002, kiskObjectId: 9001));
		Assert.True(registry.TryRemoveKisk(9001, out _));
		var expiredAfterRemoval = registry.RestoreOfflineBinding(new Player { ObjectId = 1002 });
		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.NotFound, expiredAfterRemoval.Status);
	}

	[Fact]
	public void TryRemoveKiskRemovesOfflineBindingsForCurrentMembersOnlyLikeJavaRemoveKisk()
	{
		var registry = new PlayerKiskRegistry();
		var kisk = new PlayerKiskRuntimeState(objectId: 9001, ownerObjectId: 1001, npcId: 700273);
		Assert.True(kisk.AddMember(1002));
		Assert.True(kisk.AddMember(1003));
		registry.RegisterKisk(kisk);
		Assert.True(registry.RegisterOfflineBinding(playerObjectId: 1002, kiskObjectId: 9001));
		Assert.True(registry.RegisterOfflineBinding(playerObjectId: 1003, kiskObjectId: 9001));
		Assert.True(registry.RegisterOfflineBinding(playerObjectId: 1004, kiskObjectId: 9001));

		Assert.True(registry.TryRemoveKisk(9001, out var removed));
		var memberRestore = registry.RestoreOfflineBinding(new Player { ObjectId = 1002 });
		var secondMemberRestore = registry.RestoreOfflineBinding(new Player { ObjectId = 1003 });
		var staleNonMemberRestore = registry.RestoreOfflineBinding(new Player { ObjectId = 1004 });

		Assert.Same(kisk, removed);
		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.NotFound, memberRestore.Status);
		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.NotFound, secondMemberRestore.Status);
		Assert.Equal(PlayerKiskOfflineBindingRestoreStatus.Expired, staleNonMemberRestore.Status);
		Assert.Equal(9001, staleNonMemberRestore.KiskObjectId);
	}
}
