using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RepurchasePacketSnapshotPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_BuildsEmptyRepurchasePacketSnapshotWithoutLiveState()
	{
		var plan = RepurchasePacketSnapshotPlanService.CreateDisabledPlan(
			targetObjectId: 9001,
			repurchaseItems: [],
			itemTemplates: new ItemTemplateTable([]));

		Assert.Equal(RepurchasePacketSnapshotPlanStatus.SnapshotCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.WouldQueryRepurchaseItems);
		Assert.False(plan.DidQueryRepurchaseItems);
		Assert.True(plan.WouldSendPacket);
		Assert.False(plan.DidSendPacket);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.NotNull(plan.Packet);
		Assert.Equal(Convert.FromHexString("29230000010000000000"), SerializeUnencryptedPayload(plan.Packet!));
	}

	[Fact]
	public void CreateDisabledPlan_ComposesPacketFromSuppliedRepurchaseItemsAndTemplates()
	{
		var item = new InventoryItem
		{
			ObjectId = 7001,
			ItemId = SimpleItemId,
			Count = 1,
			OwnerId = 1001,
			Location = 0,
			Slot = 65535,
		};

		var plan = RepurchasePacketSnapshotPlanService.CreateDisabledPlan(
			targetObjectId: 9001,
			repurchaseItems: [new RepurchaseSourceItem(item, RepurchasePrice: 12_345)],
			itemTemplates: new ItemTemplateTable([Template(SimpleItemId)]));

		Assert.Equal(RepurchasePacketSnapshotPlanStatus.SnapshotCreated, plan.Status);
		Assert.Empty(plan.MissingTemplateItemIds);
		Assert.Equal([item.ObjectId], plan.RepurchaseItems.Select(source => source.Item.ObjectId));
		Assert.Contains("RepurchaseService.getRepurchaseItems", plan.JavaSource, StringComparison.Ordinal);
		Assert.NotNull(plan.Packet);
		Assert.Equal(
			Convert.FromHexString("29230000010000000100591B000001E1F50524008138010000002200000100010000000000000000000000000000000000000000000000000000000012003930000000000000"),
			SerializeUnencryptedPayload(plan.Packet!));
	}

	[Fact]
	public void CreateDisabledPlan_BlocksWhenRepurchaseItemTemplateIsMissing()
	{
		var item = new InventoryItem
		{
			ObjectId = 7001,
			ItemId = SimpleItemId,
			Count = 1,
			OwnerId = 1001,
			Location = 0,
			Slot = 65535,
		};

		var plan = RepurchasePacketSnapshotPlanService.CreateDisabledPlan(
			targetObjectId: 9001,
			repurchaseItems: [new RepurchaseSourceItem(item, RepurchasePrice: 12_345)],
			itemTemplates: new ItemTemplateTable([]));

		Assert.Equal(RepurchasePacketSnapshotPlanStatus.BlockedMissingTemplate, plan.Status);
		Assert.Equal([SimpleItemId], plan.MissingTemplateItemIds);
		Assert.Null(plan.Packet);
		Assert.True(plan.WouldQueryRepurchaseItems);
		Assert.False(plan.WouldSendPacket);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static ItemTemplateSummary Template(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 40_000,
			Mask: 1,
			Level: 1,
			ItemGroup: "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0);
	}

	private const int SimpleItemId = 100000001;
}
