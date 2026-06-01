using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmRepurchaseTests
{
	[Fact]
	public void PacketOpCode_MatchesJavaServerOpcode()
	{
		// Java parity: ServerPacketsOpcodes.addPacketOpcode(167, SM_REPURCHASE.class).
		Assert.Equal(167, SmRepurchase.PacketOpCode);
	}

	[Fact]
	public void WritePayload_WritesEmptyRepurchaseListHeader()
	{
		var payload = SerializeUnencryptedPayload(new SmRepurchase(targetObjectId: 9001, items: []));
		Assert.Equal(Convert.FromHexString("29230000010000000000"), payload);

		using var reader = new PacketBuffer(payload);

		Assert.Equal(9001, reader.ReadD());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void WritePayload_WritesRepurchaseItemsWithBlobThenPrice()
	{
		var template = Template(SimpleItemId);
		var item = new InventoryItem
		{
			ObjectId = 7001,
			ItemId = SimpleItemId,
			Count = 1,
			OwnerId = 1001,
			Location = 0,
			Slot = 65535,
		};

		var payload = SerializeUnencryptedPayload(
			new SmRepurchase(
				targetObjectId: 9001,
				items: [new RepurchasePacketItem(item, template, RepurchasePrice: 12_345)]));

		Assert.Equal(
			Convert.FromHexString("29230000010000000100591B000001E1F50524008138010000002200000100010000000000000000000000000000000000000000000000000000000012003930000000000000"),
			payload);

		using var reader = new PacketBuffer(payload);

		Assert.Equal(9001, reader.ReadD());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(item.ObjectId, reader.ReadD());
		Assert.Equal(template.TemplateId, reader.ReadD());
		Assert.Equal(template.GetClientName()?.TrimEnd('\0'), reader.ReadS());

		var blobLength = reader.ReadH();
		reader.ReadB(blobLength);

		Assert.Equal(12_345, reader.ReadQ());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void WritePayload_WritesEquipmentRepurchaseItemWithEquipmentBlobThenPrice()
	{
		var template = Template(SimpleItemId, itemGroup: "SWORD", validEquipmentSlots: 3);
		var item = new InventoryItem
		{
			ObjectId = 7002,
			ItemId = SimpleItemId,
			Count = 1,
			OwnerId = 1001,
			Location = 0,
			Slot = 65535,
			Enchant = 3,
		};

		var payload = SerializeUnencryptedPayload(
			new SmRepurchase(
				targetObjectId: 9001,
				items: [new RepurchasePacketItem(item, template, RepurchasePrice: 12_345)]));

		Assert.Equal(
			Convert.FromHexString("292300000100000001005A1B000001E1F5052400813801000000CB0006000000000000000001010000000000000002000000000000000B000301E1F50500000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000100010000000000000000000000000000000000000000000000000000000012003930000000000000"),
			payload);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static ItemTemplateSummary Template(int itemId, string itemGroup = "NORMAL", long validEquipmentSlots = 0)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 40_000,
			Mask: 1,
			Level: 1,
			ItemGroup: itemGroup,
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: validEquipmentSlots);
	}

	private const int SimpleItemId = 100000001;
}
