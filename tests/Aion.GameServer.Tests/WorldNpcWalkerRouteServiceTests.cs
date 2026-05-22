using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerRouteServiceTests
{
	[Fact]
	public void ResolveRoute_ReturnsNoneWithoutWalkerId()
	{
		var service = new WorldNpcWalkerRouteService();
		var npc = CreateNpc(walkerId: "");

		var plan = service.ResolveRoute(npc, new WalkerTemplateTable([]), new WalkerVersionTable(new Dictionary<string, string>()));

		Assert.Equal(WorldNpcWalkerRouteStatus.None, plan.Status);
		Assert.Empty(plan.RouteSteps);
	}

	[Fact]
	public void ResolveRoute_ReturnsMissingForUnknownWalkerId()
	{
		var service = new WorldNpcWalkerRouteService();
		var npc = CreateNpc(walkerId: "missing-route");

		var plan = service.ResolveRoute(npc, new WalkerTemplateTable([]), new WalkerVersionTable(new Dictionary<string, string>()));

		Assert.Equal(WorldNpcWalkerRouteStatus.MissingRoute, plan.Status);
		Assert.Equal("missing-route", plan.RouteId);
	}

	[Fact]
	public void ResolveRoute_ReturnsTemplateAndVersionMetadata()
	{
		var service = new WorldNpcWalkerRouteService();
		var npc = CreateNpc(walkerId: "route-a", walkerIndex: 2);
		var routeSteps = new[]
		{
			new WalkerRouteStepSummary(1, 2, 3, 4, 0, false),
			new WalkerRouteStepSummary(5, 6, 7, 8, 1, true),
		};
		var walkerTemplates = new WalkerTemplateTable(
		[
			new WalkerTemplateSummary("route-a", 3, "SQUARE", "NORMAL", [1, 2], routeSteps),
		]);
		var walkerVersions = new WalkerVersionTable(
			new Dictionary<string, string>
			{
				["route-a"] = "route-parent",
			});

		var plan = service.ResolveRoute(npc, walkerTemplates, walkerVersions);

		Assert.Equal(WorldNpcWalkerRouteStatus.Ready, plan.Status);
		Assert.Equal("route-a", plan.RouteId);
		Assert.Equal("route-parent", plan.VersionRouteId);
		Assert.Equal(3, plan.Pool);
		Assert.Equal("SQUARE", plan.Formation);
		Assert.Equal([1, 2], plan.Rows);
		Assert.Equal(routeSteps, plan.RouteSteps);
	}

	private static WorldNpc CreateNpc(string walkerId, int walkerIndex = 0)
	{
		return new WorldNpc(
			ObjectId: 1,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				"walker",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL"),
			Position: new WorldPosition(210010000, 1, 2, 3, 0),
			WalkerId: walkerId,
			WalkerIndex: walkerIndex);
	}
}
