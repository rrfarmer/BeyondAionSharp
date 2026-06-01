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
