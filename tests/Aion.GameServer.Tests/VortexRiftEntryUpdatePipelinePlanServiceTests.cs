using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRiftEntryUpdatePipelinePlanServiceTests
{
	[Fact]
	public void CreatePlan_ComposesRemovalSyncIntoBridgeReadyMetadataWithoutDispatching()
	{
		var syncPlan = CreateSyncPlan(passedPlayerCount: 2);
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 120080000);
		var onlinePlayers = new[]
		{
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 400010000),
		};

		var plan = VortexRiftEntryUpdatePipelinePlanService.CreatePlan(
			syncPlan,
			portal,
			isMasterController: true,
			onlinePlayers,
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.Ready, plan.Status);
		Assert.True(plan.ReadyForBridge);
		Assert.Same(syncPlan, plan.SyncPlan);
		Assert.Same(portal, plan.Portal);
		Assert.Same(onlinePlayers, plan.OnlinePlayers);
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateStatus.Updated, plan.EntryUpdate.Status);
		Assert.Equal(VortexRiftEntryUpdateWorldTargetPlanStatus.Planned, plan.WorldTargetPlan.Status);
		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned, plan.PlayerTargetPlan.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.Ready, plan.CompositionPlan.Status);
		Assert.Equal([210060000, 120080000], plan.WorldIds);
		Assert.Equal([100, 101], plan.TargetPlayerObjectIds);
		Assert.True(plan.EntryUpdate.AppliedPortalSync);
		Assert.Equal(2, portal.UsedEntries);
	}

	[Fact]
	public void CreatePlan_NonMasterControllerTargetsSlaveOwnerWorldOnly()
	{
		var syncPlan = CreateSyncPlan(passedPlayerCount: 1);
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 120080000);

		var plan = VortexRiftEntryUpdatePipelinePlanService.CreatePlan(
			syncPlan,
			portal,
			isMasterController: false,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
			],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.Ready, plan.Status);
		Assert.False(plan.IsMasterController);
		Assert.Equal([120080000], plan.WorldIds);
		Assert.Equal([101], plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_MissingSyncPlanStillBuildsGuardMetadataWithoutApplyingPortalSync()
	{
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 120080000);

		var plan = VortexRiftEntryUpdatePipelinePlanService.CreatePlan(
			syncPlan: null,
			portal,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.MissingSyncPlan, plan.Status);
		Assert.False(plan.ReadyForBridge);
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateStatus.MissingSyncPlan, plan.EntryUpdate.Status);
		Assert.Equal(VortexRiftEntryUpdateWorldTargetPlanStatus.Planned, plan.WorldTargetPlan.Status);
		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned, plan.PlayerTargetPlan.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.MissingEntryUpdate, plan.CompositionPlan.Status);
		Assert.Empty(plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
		Assert.Equal(0, portal.UsedEntries);
	}

	[Fact]
	public void CreatePlan_MissingPortalBlocksEntryAndWorldTargets()
	{
		var syncPlan = CreateSyncPlan(passedPlayerCount: 2);

		var plan = VortexRiftEntryUpdatePipelinePlanService.CreatePlan(
			syncPlan,
			portal: null,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.MissingPortal, plan.Status);
		Assert.False(plan.ReadyForBridge);
		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateStatus.MissingPortal, plan.EntryUpdate.Status);
		Assert.Equal(VortexRiftEntryUpdateWorldTargetPlanStatus.MissingPortal, plan.WorldTargetPlan.Status);
		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.NoWorldTargets, plan.PlayerTargetPlan.Status);
		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.MissingEntryUpdate, plan.CompositionPlan.Status);
		Assert.Empty(plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_NoMatchingPlayersCarriesWorldMetadataButIsNotBridgeReady()
	{
		var syncPlan = CreateSyncPlan(passedPlayerCount: 2);
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 120080000);

		var plan = VortexRiftEntryUpdatePipelinePlanService.CreatePlan(
			syncPlan,
			portal,
			isMasterController: true,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 400010000)],
			() => DateTimeOffset.FromUnixTimeSeconds(2000));

		Assert.Equal(VortexRiftEntryUpdatePipelinePlanStatus.NoTargetPlayers, plan.Status);
		Assert.False(plan.ReadyForBridge);
		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.NoTargetPlayers, plan.CompositionPlan.Status);
		Assert.Equal([210060000, 120080000], plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
		Assert.Equal(2, portal.UsedEntries);
	}

	private static VortexPassedPlayerSyncPlan CreateSyncPlan(int passedPlayerCount)
	{
		return new VortexPassedPlayerSyncPlan(
			LocationId: 7,
			PassedPlayerCount: passedPlayerCount,
			UsePassedPlayerCount: true,
			JavaSource: "services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)");
	}

	private static RiftPortalState CreateVortexPortal(int masterWorldId, int slaveWorldId)
	{
		var definition = new RiftDefinition(
			1170,
			"MARCHUTAN",
			"MARCHUTAN_AM",
			"MARCHUTAN_AS",
			2,
			45,
			65,
			"ASMODIANS",
			IsVortex: true);
		var template = new NpcTemplateSummary(831143, "Vortex", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 831143,
			Template: template,
			Position: new WorldPosition(masterWorldId, 10, 20, 30, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 831144,
			Template: template,
			Position: new WorldPosition(slaveWorldId, 40, 50, 60, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 9200);
	}
}
