using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskSpawnServiceTests
{
	[Fact]
	public void CreatePlanMatchesJavaToyPetSpawnPositionOwnershipAndSourceDecrement()
	{
		var player = new Player
		{
			ObjectId = 1001,
			Position = new WorldPosition(210010000, 10.25f, 20.5f, 30.75f, 70, InstanceId: 3),
		};
		var sourceItem = CreateSourceItem(count: 3);
		var template = CreateKiskTemplate(700273);

		var plan = PlayerKiskSpawnService.CreatePlan(player, sourceItem, template, kiskObjectId: 9001);

		Assert.Equal(9001, plan.Kisk.ObjectId);
		Assert.Equal(700273, plan.Kisk.TemplateId);
		Assert.Equal(template, plan.Kisk.Template);
		Assert.Equal(new WorldPosition(210010000, 10.25f, 20.5f, 30.75f, 10, InstanceId: 3), plan.Kisk.Position);
		Assert.Equal(plan.Kisk.Position, plan.Kisk.SpawnLocation);
		Assert.Equal(new PlayerKiskOwnership(9001, 1001, 700273), plan.Ownership);
		Assert.NotNull(plan.SourceItemUpdate);
		Assert.Equal(2, plan.SourceItemUpdate.Count);
		Assert.Equal(sourceItem.ObjectId, plan.SourceItemUpdate.ObjectId);
		Assert.Null(plan.DeletedSourceItemObjectId);
	}

	[Fact]
	public void CreatePlanDeletesSingleSourceItemAndWrapsJavaHeading()
	{
		var player = new Player
		{
			ObjectId = 1001,
			Position = new WorldPosition(210010000, 1, 2, 3, 119),
		};
		var sourceItem = CreateSourceItem(count: 1);
		var template = CreateKiskTemplate(700274);

		var plan = PlayerKiskSpawnService.CreatePlan(player, sourceItem, template, kiskObjectId: 9002);

		Assert.Equal(new WorldPosition(210010000, 1, 2, 3, 59), plan.Kisk.Position);
		Assert.Null(plan.SourceItemUpdate);
		Assert.Equal(sourceItem.ObjectId, plan.DeletedSourceItemObjectId);
		Assert.Equal(new PlayerKiskOwnership(9002, 1001, 700274), plan.Ownership);
	}

	private static InventoryItem CreateSourceItem(long count)
	{
		return new InventoryItem
		{
			ObjectId = 5001,
			ItemId = 184000011,
			Count = count,
			OwnerId = 1001,
			Location = 0,
			Slot = 4,
			TuneCount = -1,
			ManaStones = [new ItemStoneSocket(167000001, 1)],
			Godstone = new PlayerGodstone(168000001, 7),
		};
	}

	private static NpcTemplateSummary CreateKiskTemplate(int npcId)
	{
		return new NpcTemplateSummary(
			npcId,
			"test_kisk",
			NameId: npcId + 100,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "PC_LIGHT_CASTLE_DOOR",
			Tribe: "KISK",
			Type: "NPC",
			MaxHp: 1000,
			Height: 2.5f,
			BoundRadius: 1.2f,
			State: WorldNpcState.DefaultSpawnState);
	}
}
