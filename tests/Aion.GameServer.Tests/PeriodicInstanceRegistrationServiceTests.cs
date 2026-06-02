using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PeriodicInstanceRegistrationServiceTests
{
	[Fact]
	public void CreateOpenRegistrationBroadcastPlan_MutatesOnceAndSendsEntryIconPlusOpeningMessageToLevelRangeLikeJava()
	{
		var service = new PeriodicInstanceRegistrationService();
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000, minLevel: 46, maxLevel: 65)]);
		var openingMessage = new SmSystemMessage(1401234);
		var eligible = CreatePlayer(objectId: 1001, level: 50);
		var lowLevel = CreatePlayer(objectId: 1002, level: 45);
		var highLevel = CreatePlayer(objectId: 1003, level: 66);

		var plan = service.CreateOpenRegistrationBroadcastPlan(
			107,
			autoGroups,
			[eligible, lowLevel, highLevel],
			openingMessage);
		var duplicate = service.CreateOpenRegistrationBroadcastPlan(107, autoGroups, [eligible]);

		Assert.True(plan.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Opened, plan.Status);
		Assert.True(plan.HasAutoGroupData);
		Assert.False(plan.WouldStopRegistrationsByMaskId);
		Assert.True(service.IsRegistrationOpen(107));
		var broadcast = Assert.Single(plan.PlayerBroadcasts);
		Assert.Equal(eligible.ObjectId, broadcast.PlayerObjectId);
		Assert.Collection(
			broadcast.Packets,
			packet =>
			{
				var autoGroupPacket = Assert.IsType<SmAutoGroup>(packet);
				Assert.Equal(107, autoGroupPacket.MaskId);
				Assert.Equal(SmAutoGroup.EntryIconWindowId, autoGroupPacket.WindowId);
				Assert.False(autoGroupPacket.IsClosed);
			},
			packet =>
			{
				var systemMessage = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1401234, systemMessage.MessageId);
			});
		Assert.False(duplicate.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.AlreadyOpen, duplicate.Status);
		Assert.Empty(duplicate.PlayerBroadcasts);
	}

	[Fact]
	public void CreateCloseRegistrationBroadcastPlan_MutatesOnceAndMarksStopRegistrationsLikeJava()
	{
		var service = new PeriodicInstanceRegistrationService();
		var autoGroups = new AutoGroupTable([CreateAutoGroup(108, 300120000, minLevel: 46, maxLevel: 65)]);
		var eligible = CreatePlayer(objectId: 2001, level: 50);
		var lowLevel = CreatePlayer(objectId: 2002, level: 45);
		Assert.True(service.OpenRegistration(108));

		var plan = service.CreateCloseRegistrationBroadcastPlan(108, autoGroups, [eligible, lowLevel]);
		var duplicate = service.CreateCloseRegistrationBroadcastPlan(108, autoGroups, [eligible]);

		Assert.True(plan.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Closed, plan.Status);
		Assert.True(plan.HasAutoGroupData);
		Assert.True(plan.WouldStopRegistrationsByMaskId);
		Assert.False(service.IsRegistrationOpen(108));
		var broadcast = Assert.Single(plan.PlayerBroadcasts);
		Assert.Equal(eligible.ObjectId, broadcast.PlayerObjectId);
		var autoGroupPacket = Assert.IsType<SmAutoGroup>(Assert.Single(broadcast.Packets));
		Assert.Equal(108, autoGroupPacket.MaskId);
		Assert.Equal(SmAutoGroup.EntryIconWindowId, autoGroupPacket.WindowId);
		Assert.True(autoGroupPacket.IsClosed);
		Assert.False(duplicate.Changed);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.NotOpen, duplicate.Status);
		Assert.False(duplicate.WouldStopRegistrationsByMaskId);
		Assert.Empty(duplicate.PlayerBroadcasts);
	}

	[Fact]
	public void CreateRegistrationBroadcastPlan_PreservesStateChangeWhenAutoGroupDataMissingLikeJavaUnknownTypeNoBroadcast()
	{
		var service = new PeriodicInstanceRegistrationService();

		var openPlan = service.CreateOpenRegistrationBroadcastPlan(999, autoGroups: null, [CreatePlayer(level: 50)]);
		var closePlan = service.CreateCloseRegistrationBroadcastPlan(999, autoGroups: null, [CreatePlayer(level: 50)]);

		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Opened, openPlan.Status);
		Assert.False(openPlan.HasAutoGroupData);
		Assert.Empty(openPlan.PlayerBroadcasts);
		Assert.Equal(PeriodicInstanceRegistrationBroadcastStatus.Closed, closePlan.Status);
		Assert.False(closePlan.HasAutoGroupData);
		Assert.True(closePlan.WouldStopRegistrationsByMaskId);
		Assert.Empty(closePlan.PlayerBroadcasts);
	}

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

	private static Player CreatePlayer(int level, int objectId = 1001)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Level{level}",
			Race = "ELYOS",
			Level = level,
		};
	}
}
