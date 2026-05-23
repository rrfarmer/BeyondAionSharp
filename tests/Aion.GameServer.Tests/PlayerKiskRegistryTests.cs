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

		var replacement = registry.RegisterKisk(ownerObjectId: 1001, kiskObjectId: 9002, npcId: 700274);

		Assert.True(registry.HaveKisk(1001));
		Assert.Equal(replacement, registry.GetOwnerKisk(1001));
		Assert.False(registry.RemoveKisk(9001));
		Assert.True(registry.RemoveKisk(9002));
		Assert.False(registry.HaveKisk(1001));
		Assert.Null(registry.GetOwnerKisk(1001));
	}

	[Fact]
	public void RuntimeContextExposesKiskRegistry()
	{
		var runtimeContext = new GameServerRuntimeContext();

		runtimeContext.Kisks.RegisterKisk(ownerObjectId: 1001, kiskObjectId: 9001, npcId: 700273);

		Assert.True(runtimeContext.Kisks.HaveKisk(1001));
	}
}
