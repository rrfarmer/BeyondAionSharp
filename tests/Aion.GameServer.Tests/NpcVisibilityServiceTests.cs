using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class NpcVisibilityServiceTests
{
	[Fact]
	public void UpdateKnownNpcs_TracksAppearedAndDisappearedNpcObjects()
	{
		var service = new NpcVisibilityService();
		var player = new Player
		{
			ObjectId = 1001,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var visibleNpc = CreateNpc(5001, new WorldPosition(210010000, 10, 0, 0, 0));
		var distantNpc = CreateNpc(5002, new WorldPosition(210010000, 200, 0, 0, 0));
		var otherMapNpc = CreateNpc(5003, new WorldPosition(220010000, 10, 0, 0, 0));

		var first = service.UpdateKnownNpcs(player, [visibleNpc, distantNpc, otherMapNpc]);
		var second = service.UpdateKnownNpcs(player, [visibleNpc, distantNpc, otherMapNpc]);
		player.Position = player.Position with { X = 200 };
		var third = service.UpdateKnownNpcs(player, [visibleNpc, distantNpc, otherMapNpc]);
		var knowsVisibleAfterThird = service.IsKnownNpc(player, visibleNpc.ObjectId);
		var knowsDistantAfterThird = service.IsKnownNpc(player, distantNpc.ObjectId);
		service.ClearKnownNpcs(player.ObjectId);
		player.Position = player.Position with { X = 0 };
		var afterClear = service.UpdateKnownNpcs(player, [visibleNpc]);

		Assert.Equal([visibleNpc], first.Appeared);
		Assert.Empty(first.DisappearedObjectIds);
		Assert.True(service.IsKnownNpc(player.ObjectId, visibleNpc.ObjectId));
		Assert.False(service.IsKnownNpc(player.ObjectId, distantNpc.ObjectId));
		Assert.Empty(second.Appeared);
		Assert.Empty(second.DisappearedObjectIds);
		Assert.Equal([distantNpc], third.Appeared);
		Assert.Equal([visibleNpc.ObjectId], third.DisappearedObjectIds);
		Assert.False(knowsVisibleAfterThird);
		Assert.True(knowsDistantAfterThird);
		Assert.Equal([visibleNpc], afterClear.Appeared);
		Assert.True(service.IsKnownNpc(player, visibleNpc.ObjectId));
	}

	[Fact]
	public void UpdateKnownNpcs_RemovedKiskSnapshotOnlyDeletesForViewerThatKnewIt()
	{
		var service = new NpcVisibilityService();
		var kisk = CreateNpc(9001, new WorldPosition(210010000, 10, 0, 0, 0));
		var nearViewer = new Player
		{
			ObjectId = 1001,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var distantViewer = new Player
		{
			ObjectId = 1002,
			Position = new WorldPosition(210010000, 300, 0, 0, 0),
		};

		var nearFirstRefresh = service.UpdateKnownNpcs(nearViewer, [kisk]);
		var distantFirstRefresh = service.UpdateKnownNpcs(distantViewer, [kisk]);
		var nearRemovalRefresh = service.UpdateKnownNpcs(nearViewer, []);
		var distantRemovalRefresh = service.UpdateKnownNpcs(distantViewer, []);

		Assert.Equal([kisk], nearFirstRefresh.Appeared);
		Assert.Empty(distantFirstRefresh.Appeared);
		Assert.Equal([kisk.ObjectId], nearRemovalRefresh.DisappearedObjectIds);
		Assert.Empty(distantRemovalRefresh.DisappearedObjectIds);
		Assert.False(service.IsKnownNpc(nearViewer, kisk.ObjectId));
		Assert.False(service.IsKnownNpc(distantViewer, kisk.ObjectId));
	}

	private static WorldNpc CreateNpc(int objectId, WorldPosition position)
	{
		var template = new NpcTemplateSummary(
			203000 + objectId,
			"visible-npc",
			NameId: 1,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL");
		return new WorldNpc(objectId, template.TemplateId, template, position);
	}
}
