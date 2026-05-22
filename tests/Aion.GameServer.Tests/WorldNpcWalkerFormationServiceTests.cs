using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerFormationServiceTests
{
	[Fact]
	public void FormSquareGroup_FormsLineRowsLikeJavaWalkerGroup()
	{
		var service = new WorldNpcWalkerFormationService();
		var npcs = new[]
		{
			CreateNpc(objectId: 1, walkerIndex: 1),
			CreateNpc(objectId: 2, walkerIndex: 3),
			CreateNpc(objectId: 3, walkerIndex: 2),
		};
		var plan = CreatePlan(rows: [3]);

		var result = service.FormSquareGroup(npcs, plan);

		Assert.Equal(WorldNpcWalkerFormationStatus.Ready, result.Status);
		Assert.Equal("route-parent", result.VersionRouteId);
		Assert.Equal([2, 3, 1], result.Members.Select(member => member.ObjectId).ToArray());
		AssertFormationMember(result.Members[0], objectId: 2, sagittal: -2, coronal: 0, x: 0, y: 2);
		AssertFormationMember(result.Members[1], objectId: 3, sagittal: 0, coronal: 0, x: 0, y: 0);
		AssertFormationMember(result.Members[2], objectId: 1, sagittal: 2, coronal: 0, x: 0, y: -2);
	}

	[Fact]
	public void FormSquareGroup_FormsMultiRowSquareLikeJavaWalkerGroup()
	{
		var service = new WorldNpcWalkerFormationService();
		var npcs = new[]
		{
			CreateNpc(objectId: 1, walkerIndex: 1),
			CreateNpc(objectId: 2, walkerIndex: 2),
			CreateNpc(objectId: 3, walkerIndex: 3),
		};
		var plan = CreatePlan(rows: [1, 2]);

		var result = service.FormSquareGroup(npcs, plan);

		Assert.Equal(WorldNpcWalkerFormationStatus.Ready, result.Status);
		AssertFormationMember(result.Members[0], objectId: 3, sagittal: 0, coronal: -1.7320508f, x: 0, y: 0);
		AssertFormationMember(result.Members[1], objectId: 2, sagittal: -1, coronal: 0, x: 0, y: 1);
		AssertFormationMember(result.Members[2], objectId: 1, sagittal: 1, coronal: 0, x: 0, y: -1);
	}

	[Fact]
	public void FormSquareGroup_LeavesPointFormationsUnchanged()
	{
		var service = new WorldNpcWalkerFormationService();
		var npcs = new[]
		{
			CreateNpc(objectId: 1, walkerIndex: 1, x: 7, y: 8),
		};
		var plan = CreatePlan(formation: "POINT", rows: []);

		var result = service.FormSquareGroup(npcs, plan);

		Assert.Equal(WorldNpcWalkerFormationStatus.PointFormation, result.Status);
		var member = Assert.Single(result.Members);
		AssertFormationMember(member, objectId: 1, sagittal: 0, coronal: 0, x: 7, y: 8);
	}

	[Fact]
	public void FormSquareGroup_RequiresSecondRouteStep()
	{
		var service = new WorldNpcWalkerFormationService();
		var npcs = new[]
		{
			CreateNpc(objectId: 1, walkerIndex: 1, x: 7, y: 8),
		};
		var plan = CreatePlan(rows: [1], routeSteps: [new WalkerRouteStepSummary(0, 0, 0, 0, 0, true)]);

		var result = service.FormSquareGroup(npcs, plan);

		Assert.Equal(WorldNpcWalkerFormationStatus.InsufficientRoute, result.Status);
		var member = Assert.Single(result.Members);
		AssertFormationMember(member, objectId: 1, sagittal: 0, coronal: 0, x: 7, y: 8);
	}

	[Fact]
	public void GetLinePoint_ProjectsDiagonalShiftsLikeJavaWalkerGroup()
	{
		var point = WorldNpcWalkerFormationService.GetLinePoint(
			new WalkerPoint(0, 0),
			new WalkerPoint(10, 10),
			new WorldNpcWalkerShift(2, 2));

		Assert.InRange(point.X, 2.82f, 2.84f);
		Assert.InRange(point.Y, 0f, 0.01f);
	}

	private static WorldNpcWalkerRoutePlan CreatePlan(
		string formation = "SQUARE",
		IReadOnlyList<int>? rows = null,
		IReadOnlyList<WalkerRouteStepSummary>? routeSteps = null)
	{
		return WorldNpcWalkerRoutePlan.Ready(
			"route-a",
			"route-parent",
			formation,
			rows ?? [1],
			routeSteps ??
			[
				new WalkerRouteStepSummary(0, 0, 0, 0, 0, false),
				new WalkerRouteStepSummary(10, 0, 0, 0, 1, true),
			]);
	}

	private static WorldNpc CreateNpc(int objectId, int walkerIndex, float x = 0, float y = 0)
	{
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
			Position: new WorldPosition(210010000, x, y, 3, 0),
			WalkerId: "route-a",
			WalkerIndex: walkerIndex);
	}

	private static void AssertFormationMember(
		WorldNpcWalkerFormationMember member,
		int objectId,
		float sagittal,
		float coronal,
		float x,
		float y)
	{
		Assert.Equal(objectId, member.ObjectId);
		Assert.Equal(sagittal, member.SagittalShift, precision: 4);
		Assert.Equal(coronal, member.CoronalShift, precision: 4);
		Assert.Equal(x, member.X, precision: 4);
		Assert.Equal(y, member.Y, precision: 4);
	}
}
