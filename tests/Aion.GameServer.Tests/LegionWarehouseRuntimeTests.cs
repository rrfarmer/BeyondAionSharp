using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class LegionWarehouseRuntimeTests
{
	[Fact]
	public void InUseStateMatchesJavaCompareAndSetSemantics()
	{
		var runtime = new LegionWarehouseRuntime();

		Assert.True(runtime.TrySetInUse(legionId: 77, playerObjectId: 1001));
		Assert.Equal(1001, runtime.GetCurrentUser(77));
		Assert.False(runtime.TrySetInUse(legionId: 77, playerObjectId: 1002));
		Assert.False(runtime.UnsetInUse(legionId: 77, playerObjectId: 1002));
		Assert.Equal(1001, runtime.GetCurrentUser(77));
		Assert.True(runtime.UnsetInUse(legionId: 77, playerObjectId: 1001));
		Assert.Equal(0, runtime.GetCurrentUser(77));
		Assert.True(runtime.TrySetInUse(legionId: 77, playerObjectId: 1002));
		Assert.Equal(1002, runtime.GetCurrentUser(77));
	}
}
