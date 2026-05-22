using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerFormationOrganizerServiceTests
{
	[Fact]
	public void Organize_CreatesActiveFormationAndSpawnsUnalignedRemainder()
	{
		var service = new WorldNpcWalkerFormationOrganizerService();
		var templates = CreateWalkerTemplates(
			new WalkerTemplateSummary("route-a", 4, "SQUARE", "NORMAL", [3], CreateRouteSteps()));
		var npcs = new[]
		{
			CreateNpc(1, "route-a", walkerIndex: 1),
			CreateNpc(2, "route-a", walkerIndex: 2),
			CreateNpc(3, "route-a", walkerIndex: 3),
			CreateNpc(4, "route-a", walkerIndex: 4, x: 10, y: 10),
		};

		var result = service.Organize(npcs, templates, EmptyVersions());

		Assert.Empty(result.Warnings);
		var formation = Assert.Single(result.ActiveFormations);
		Assert.Equal(WorldNpcWalkerFormationStatus.Ready, formation.Status);
		Assert.Equal("route-a", formation.RouteId);
		Assert.Equal([3, 2, 1], formation.Members.Select(member => member.ObjectId).ToArray());
		var activeWalker = Assert.Single(result.ActiveWalkers);
		Assert.Equal(4, activeWalker.ObjectId);
		Assert.Empty(result.FormationVariants);
		Assert.Empty(result.WalkerVariants);
	}

	[Fact]
	public void Organize_StoresVersionedFormationVariants()
	{
		var service = new WorldNpcWalkerFormationOrganizerService();
		var templates = CreateWalkerTemplates(
			new WalkerTemplateSummary("route-v", 2, "SQUARE", "NORMAL", [2], CreateRouteSteps()));
		var versions = new WalkerVersionTable(
			new Dictionary<string, string>
			{
				["route-v"] = "route-parent",
			});
		var npcs = new[]
		{
			CreateNpc(1, "route-v", walkerIndex: 1),
			CreateNpc(2, "route-v", walkerIndex: 2),
		};

		var result = service.Organize(npcs, templates, versions);

		Assert.Empty(result.ActiveFormations);
		Assert.Empty(result.ActiveWalkers);
		var variants = Assert.Single(result.FormationVariants);
		Assert.Equal("route-parent", variants.Key);
		var variant = Assert.Single(variants.Value);
		Assert.Equal("route-v", variant.RouteId);
		Assert.Equal("route-parent", variant.VersionRouteId);
		Assert.Equal([2, 1], variant.Members.Select(member => member.ObjectId).ToArray());
	}

	[Fact]
	public void Organize_StoresSingleVersionedWalkerVariants()
	{
		var service = new WorldNpcWalkerFormationOrganizerService();
		var templates = CreateWalkerTemplates(
			new WalkerTemplateSummary("route-single", 1, "POINT", "NORMAL", [], CreateRouteSteps()));
		var versions = new WalkerVersionTable(
			new Dictionary<string, string>
			{
				["route-single"] = "single-parent",
			});

		var result = service.Organize(
			[CreateNpc(10, "route-single", walkerIndex: 0, x: 3, y: 4)],
			templates,
			versions);

		Assert.Empty(result.ActiveFormations);
		Assert.Empty(result.ActiveWalkers);
		var variants = Assert.Single(result.WalkerVariants);
		Assert.Equal("single-parent", variants.Key);
		var walker = Assert.Single(variants.Value);
		Assert.Equal(10, walker.ObjectId);
		Assert.Equal("route-single", walker.RouteId);
		Assert.Equal("single-parent", walker.VersionRouteId);
		Assert.Equal(3, walker.X);
		Assert.Equal(4, walker.Y);
	}

	[Fact]
	public void Organize_SpawnsUnalignedSinglesAndWarnsLikeJava()
	{
		var service = new WorldNpcWalkerFormationOrganizerService();
		var templates = CreateWalkerTemplates(
			new WalkerTemplateSummary("route-a", 2, "SQUARE", "NORMAL", [2], CreateRouteSteps()));
		var npcs = new[]
		{
			CreateNpc(1, "route-a", walkerIndex: 1, x: 0, y: 0),
			CreateNpc(2, "route-a", walkerIndex: 2, x: 5, y: 5),
		};

		var result = service.Organize(npcs, templates, EmptyVersions());

		Assert.Equal([1, 2], result.ActiveWalkers.Select(walker => walker.ObjectId).ToArray());
		var warning = Assert.Single(result.Warnings);
		Assert.Equal(WorldNpcWalkerOrganizationWarningKind.WalkersNotAligned, warning.Kind);
		Assert.Equal("route-a", warning.RouteId);
		Assert.Equal([1, 2], warning.ObjectIds);
		Assert.Empty(result.ActiveFormations);
	}

	[Fact]
	public void Organize_WarnsWhenTemplatePoolDoesNotMatchCandidates()
	{
		var service = new WorldNpcWalkerFormationOrganizerService();
		var templates = CreateWalkerTemplates(
			new WalkerTemplateSummary("route-a", 2, "SQUARE", "NORMAL", [3], CreateRouteSteps()));
		var npcs = new[]
		{
			CreateNpc(1, "route-a", walkerIndex: 1),
			CreateNpc(2, "route-a", walkerIndex: 2),
			CreateNpc(3, "route-a", walkerIndex: 3),
		};

		var result = service.Organize(npcs, templates, EmptyVersions());

		var warning = Assert.Single(result.Warnings);
		Assert.Equal(WorldNpcWalkerOrganizationWarningKind.IncorrectPool, warning.Kind);
		Assert.Equal(2, warning.ExpectedPool);
		Assert.Equal(3, warning.ActualPool);
		Assert.Equal([1, 2, 3], warning.ObjectIds);
		Assert.Single(result.ActiveFormations);
	}

	[Fact]
	public void Organize_GroupsByOriginalSpawnPositionAfterRuntimePlacement()
	{
		var service = new WorldNpcWalkerFormationOrganizerService();
		var templates = CreateWalkerTemplates(
			new WalkerTemplateSummary("route-a", 2, "SQUARE", "NORMAL", [2], CreateRouteSteps()));
		var npcs = new[]
		{
			CreateNpc(1, "route-a", walkerIndex: 1, x: 10, y: 20, spawnX: 0, spawnY: 0),
			CreateNpc(2, "route-a", walkerIndex: 2, x: 11, y: 21, spawnX: 0, spawnY: 0),
		};

		var result = service.Organize(npcs, templates, EmptyVersions());

		Assert.Empty(result.Warnings);
		var formation = Assert.Single(result.ActiveFormations);
		Assert.Equal([2, 1], formation.Members.Select(member => member.ObjectId).ToArray());
		Assert.Equal(0, formation.Members[0].X, precision: 4);
	}

	private static WalkerTemplateTable CreateWalkerTemplates(params WalkerTemplateSummary[] templates)
	{
		return new WalkerTemplateTable(templates);
	}

	private static WalkerVersionTable EmptyVersions()
	{
		return new WalkerVersionTable(new Dictionary<string, string>());
	}

	private static IReadOnlyList<WalkerRouteStepSummary> CreateRouteSteps()
	{
		return
		[
			new WalkerRouteStepSummary(0, 0, 0, 0, 0, false),
			new WalkerRouteStepSummary(10, 0, 0, 0, 1, true),
		];
	}

	private static WorldNpc CreateNpc(
		int objectId,
		string walkerId,
		int walkerIndex,
		float x = 0,
		float y = 0,
		float? spawnX = null,
		float? spawnY = null)
	{
		var position = new WorldPosition(210010000, x, y, 3, 0);
		return new WorldNpc(
			ObjectId: objectId,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				$"walker-{objectId}",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL"),
			Position: position,
			WalkerId: walkerId,
			WalkerIndex: walkerIndex,
			SpawnPosition: spawnX.HasValue || spawnY.HasValue ? new WorldPosition(210010000, spawnX ?? x, spawnY ?? y, 3, 0) : null);
	}
}
