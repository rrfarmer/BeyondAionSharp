using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRiftEntryUpdateWorldTargetPlanServiceTests
{
	[Fact]
	public void CreatePlan_MasterControllerTargetsOwnerAndSlaveWorldLikeJavaGetWorldsList()
	{
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 120080000);

		var plan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal, isMasterController: true);

		Assert.Equal(VortexRiftEntryUpdateWorldTargetPlanStatus.Planned, plan.Status);
		Assert.True(plan.IsMasterController);
		Assert.Same(portal, plan.Portal);
		Assert.Equal([210060000, 120080000], plan.WorldIds);
	}

	[Fact]
	public void CreatePlan_NonMasterControllerTargetsOnlyOwnerWorldLikeJavaGetWorldsList()
	{
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 120080000);

		var plan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal, isMasterController: false);

		Assert.Equal(VortexRiftEntryUpdateWorldTargetPlanStatus.Planned, plan.Status);
		Assert.False(plan.IsMasterController);
		Assert.Equal([120080000], plan.WorldIds);
	}

	[Fact]
	public void CreatePlan_MasterControllerPreservesDuplicateWorldIdsLikeJavaArray()
	{
		var portal = CreateVortexPortal(masterWorldId: 210060000, slaveWorldId: 210060000);

		var plan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal, isMasterController: true);

		Assert.Equal([210060000, 210060000], plan.WorldIds);
	}

	[Fact]
	public void CreatePlan_MissingPortalProducesMetadataOnlyPlan()
	{
		var plan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal: null, isMasterController: true);

		Assert.Equal(VortexRiftEntryUpdateWorldTargetPlanStatus.MissingPortal, plan.Status);
		Assert.True(plan.IsMasterController);
		Assert.Null(plan.Portal);
		Assert.Empty(plan.WorldIds);
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
