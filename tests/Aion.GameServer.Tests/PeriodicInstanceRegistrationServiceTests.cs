using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PeriodicInstanceRegistrationServiceTests
{
	[Fact]
	public void CreateOpenRegistrationPackets_FiltersByLevelAndPortalCooldownLikeJavaPeriodicInstanceManager()
	{
		var service = new PeriodicInstanceRegistrationService();
		Assert.True(service.OpenRegistration(107));
		Assert.False(service.OpenRegistration(107));
		Assert.True(service.OpenRegistration(108));
		var autoGroups = new AutoGroupTable(
		[
			CreateAutoGroup(107, 300110000, minLevel: 46, maxLevel: 65),
			CreateAutoGroup(108, 300120000, minLevel: 46, maxLevel: 65),
			CreateAutoGroup(109, 300130000, minLevel: 46, maxLevel: 65),
		]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(1, 300110000, "PC_ALL", MaxCount: 1),
			new InstanceCooltimeSummary(2, 300120000, "PC_ALL", MaxCount: 1),
		]);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var eligible = CreatePlayer(level: 50);
		var lowLevel = CreatePlayer(level: 45);
		var onCooldown = CreatePlayer(level: 50);
		onCooldown.PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
		{
			[300110000] = new(300110000, ReuseTimeMillis: 200_000, EntryCount: 1),
			[300120000] = new(300120000, ReuseTimeMillis: 200_000, EntryCount: 1),
		};

		var eligiblePackets = service.CreateOpenRegistrationPackets(eligible, autoGroups, cooltimes, now);
		var lowLevelPackets = service.CreateOpenRegistrationPackets(lowLevel, autoGroups, cooltimes, now);
		var cooldownPackets = service.CreateOpenRegistrationPackets(onCooldown, autoGroups, cooltimes, now);

		Assert.Equal([107, 108], eligiblePackets.Select(packet => packet.MaskId).OrderBy(maskId => maskId));
		Assert.Empty(lowLevelPackets);
		Assert.Empty(cooldownPackets);
		Assert.True(service.CloseRegistration(108));
		Assert.False(service.CloseRegistration(108));
		Assert.True(service.IsRegistrationOpen(107));
		Assert.False(service.IsRegistrationOpen(108));
	}

	private static AutoGroupSummary CreateAutoGroup(int maskId, int worldId, int minLevel, int maxLevel)
	{
		return new AutoGroupSummary(
			maskId,
			worldId,
			NameId: 140000 + maskId,
			TitleId: 150000 + maskId,
			minLevel,
			maxLevel,
			RegisterQuick: true,
			RegisterGroup: true,
			RegisterNew: true,
			NpcIds: []);
	}

	private static Player CreatePlayer(int level)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = $"Level{level}",
			Race = "ELYOS",
			Level = level,
		};
	}
}
