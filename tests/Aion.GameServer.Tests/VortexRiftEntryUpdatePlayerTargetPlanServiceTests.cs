using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexRiftEntryUpdatePlayerTargetPlanServiceTests
{
	[Fact]
	public void CreatePlan_MasterWorldTargetsPlayersInJavaWorldLoopOrder()
	{
		var worldTargetPlan = CreateWorldTargetPlan(masterWorldId: 210060000, slaveWorldId: 120080000, isMasterController: true);
		var onlinePlayers = new[]
		{
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 400010000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 120080000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(103, 210060000),
		};

		var plan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(worldTargetPlan, onlinePlayers);

		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned, plan.Status);
		Assert.Same(worldTargetPlan, plan.WorldTargetPlan);
		Assert.Equal([100, 103, 102], plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_NonMasterWorldTargetsOnlyPlayersInOwnerWorld()
	{
		var worldTargetPlan = CreateWorldTargetPlan(masterWorldId: 210060000, slaveWorldId: 120080000, isMasterController: false);
		var onlinePlayers = new[]
		{
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(102, 120080000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(103, 120080000),
		};

		var plan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(worldTargetPlan, onlinePlayers);

		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned, plan.Status);
		Assert.Equal([102, 103], plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_PreservesDuplicateWorldTargetsLikeJavaSendRiftInfoLoop()
	{
		var worldTargetPlan = CreateWorldTargetPlan(masterWorldId: 210060000, slaveWorldId: 210060000, isMasterController: true);
		var onlinePlayers = new[]
		{
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000),
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(101, 210060000),
		};

		var plan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(worldTargetPlan, onlinePlayers);

		Assert.Equal([100, 101, 100, 101], plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_NoMatchingPlayersStillProducesPlannedEmptyTargetList()
	{
		var worldTargetPlan = CreateWorldTargetPlan(masterWorldId: 210060000, slaveWorldId: 120080000, isMasterController: true);
		var onlinePlayers = new[]
		{
			new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 400010000),
		};

		var plan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(worldTargetPlan, onlinePlayers);

		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned, plan.Status);
		Assert.Empty(plan.TargetPlayerObjectIds);
	}

	[Fact]
	public void CreatePlan_GuardsMissingPlanNoWorldTargetsAndNoOnlinePlayers()
	{
		var noPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			worldTargetPlan: null,
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);
		var missingWorlds = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal: null, isMasterController: true),
			[new VortexRiftEntryUpdateOnlinePlayerSnapshot(100, 210060000)]);
		var noPlayers = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(
			CreateWorldTargetPlan(masterWorldId: 210060000, slaveWorldId: 120080000, isMasterController: true),
			[]);

		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.MissingWorldTargetPlan, noPlan.Status);
		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.NoWorldTargets, missingWorlds.Status);
		Assert.Equal(VortexRiftEntryUpdatePlayerTargetPlanStatus.NoOnlinePlayers, noPlayers.Status);
		Assert.Empty(noPlan.TargetPlayerObjectIds);
		Assert.Empty(missingWorlds.TargetPlayerObjectIds);
		Assert.Empty(noPlayers.TargetPlayerObjectIds);
	}

	private static VortexRiftEntryUpdateWorldTargetPlan CreateWorldTargetPlan(
		int masterWorldId,
		int slaveWorldId,
		bool isMasterController)
	{
		return VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(
			CreateVortexPortal(masterWorldId, slaveWorldId),
			isMasterController);
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
