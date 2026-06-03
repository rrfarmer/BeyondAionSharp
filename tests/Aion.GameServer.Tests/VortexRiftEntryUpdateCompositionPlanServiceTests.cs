using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRiftEntryUpdateCompositionPlanServiceTests
{
	[Fact]
	public void CreatePlan_ComposesReadyDispatchMetadataWithoutCallingDispatcher()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(entryUpdate.Portal, isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan,
			[
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 120080000),
				new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 400010000),
			]);

		var plan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.Ready, plan.Status);
		Assert.True(plan.HasPacketIntent);
		Assert.True(plan.ReadyForDispatch);
		Assert.Same(entryUpdate, plan.EntryUpdate);
		Assert.Same(worldTargetPlan, plan.WorldTargetPlan);
		Assert.Same(playerTargetPlan, plan.PlayerTargetPlan);
		Assert.Same(entryUpdate.Packet, plan.Packet);
		Assert.Equal([210060000, 120080000], plan.WorldIds);
		Assert.Equal([100, 101], plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_MissingEntryUpdateBlocksDispatchMetadata()
	{
		var entryUpdate = CreateMissingPortalEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(CreateVortexPortal(), isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		var plan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.MissingEntryUpdate, plan.Status);
		Assert.False(plan.HasPacketIntent);
		Assert.False(plan.ReadyForDispatch);
		Assert.Empty(plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_MissingWorldTargetsBlocksDispatchMetadata()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal: null, isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);

		var plan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.MissingWorldTargets, plan.Status);
		Assert.True(plan.HasPacketIntent);
		Assert.False(plan.ReadyForDispatch);
		Assert.Empty(plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_MissingPlayerTargetsBlocksDispatchMetadata()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(entryUpdate.Portal, isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(worldTargetPlan, []);

		var plan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.MissingPlayerTargets, plan.Status);
		Assert.True(plan.HasPacketIntent);
		Assert.False(plan.ReadyForDispatch);
		Assert.Equal([210060000, 120080000], plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_MismatchedWorldAndPlayerTargetPlansBlocksDispatchMetadata()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(entryUpdate.Portal, isMasterController: true);
		var otherWorldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(CreateVortexPortal(), isMasterController: false);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			otherWorldTargetPlan,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 120080000)]);

		var plan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.MismatchedTargetPlans, plan.Status);
		Assert.False(plan.ReadyForDispatch);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_NoTargetPlayersCarriesWorldMetadataButBlocksDispatch()
	{
		var entryUpdate = CreateEntryUpdate();
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(entryUpdate.Portal, isMasterController: true);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 400010000)]);

		var plan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		Assert.Equal(VortexRiftEntryUpdateCompositionPlanStatus.NoTargetPlayers, plan.Status);
		Assert.True(plan.HasPacketIntent);
		Assert.False(plan.ReadyForDispatch);
		Assert.Equal([210060000, 120080000], plan.WorldIds);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateResult CreateEntryUpdate()
	{
		return VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(
			new VortexPassedPlayerSyncPlan(
				LocationId: 0,
				PassedPlayerCount: 2,
				UsePassedPlayerCount: true,
				"controllers/RVController.syncPassed(true)"),
			CreateVortexPortal(),
			() => DateTimeOffset.FromUnixTimeSeconds(2000));
	}

	private static VortexPassedPlayerSyncRiftEntryUpdateResult CreateMissingPortalEntryUpdate()
	{
		return VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(
			new VortexPassedPlayerSyncPlan(
				LocationId: 0,
				PassedPlayerCount: 2,
				UsePassedPlayerCount: true,
				"controllers/RVController.syncPassed(true)"),
			portal: null);
	}

	private static RiftPortalState CreateVortexPortal()
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
			Position: new WorldPosition(210060000, 10, 20, 30, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 831144,
			Template: template,
			Position: new WorldPosition(120080000, 40, 50, 60, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 9200);
	}
}
