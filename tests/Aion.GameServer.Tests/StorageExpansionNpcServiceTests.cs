using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class StorageExpansionNpcServiceTests
{
	[Fact]
	public void RequestCubeExpansion_RegistersJavaSharedQuestion()
	{
		var player = CreatePlayer();
		var npc = CreateNpc(798008);
		var service = new StorageExpansionNpcService();

		var result = service.RequestCubeExpansion(
			player,
			npc,
			CreateTemplate(level: 1, price: 1000),
			cubeExpansionLimit: 11,
			npcCubeExpandsSizeLimit: 5);

		Assert.True(result.Handled);
		Assert.Equal(StorageExpansionRequestStatus.Requested, result.Status);
		Assert.NotNull(player.PendingStorageExpansionRequest);
		Assert.Equal(InventoryExpansionStorage.Cube, player.PendingStorageExpansionRequest.Storage);
		Assert.Equal(1, player.PendingStorageExpansionRequest.TargetNpcExpands);
		Assert.Equal(1000, player.PendingStorageExpansionRequest.Price);
		Assert.Equal(SmQuestionWindow.WarehouseExpandWarning, result.QuestionWindow?.Code);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void RequestWarehouseExpansion_DuplicateQuestionKeepsOriginalPendingRequest()
	{
		var player = CreatePlayer();
		var service = new StorageExpansionNpcService();
		var first = service.RequestWarehouseExpansion(player, CreateNpc(203199), CreateTemplate(level: 1, price: 1200));

		var duplicate = service.RequestWarehouseExpansion(player, CreateNpc(203687, objectId: 9002), CreateTemplate(level: 1, price: 1200));

		Assert.Equal(StorageExpansionRequestStatus.Requested, first.Status);
		Assert.False(duplicate.Handled);
		Assert.Equal(StorageExpansionRequestStatus.DuplicateQuestion, duplicate.Status);
		Assert.Equal(203199, player.PendingStorageExpansionRequest?.NpcTemplateId);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void RequestExpansion_CannotExpandEmitsJavaCapMessages()
	{
		var player = CreatePlayer();
		player.NpcExpands = 11;
		var service = new StorageExpansionNpcService();

		var cube = service.RequestCubeExpansion(
			player,
			CreateNpc(798008),
			CreateTemplate(level: 12, price: 1000),
			cubeExpansionLimit: 11,
			npcCubeExpandsSizeLimit: 11);
		var warehouse = service.RequestWarehouseExpansion(
			new Player { WarehouseNpcExpands = 11 },
			CreateNpc(203199),
			CreateTemplate(level: 12, price: 1200));

		Assert.Equal(StorageExpansionRequestStatus.CannotExpand, cube.Status);
		Assert.IsType<SmSystemMessage>(Assert.Single(cube.Packets));
		Assert.Equal(StorageExpansionRequestStatus.CannotExpand, warehouse.Status);
		Assert.IsType<SmSystemMessage>(Assert.Single(warehouse.Packets));
	}

	[Fact]
	public void RequestExpansion_BelowNpcMinimumEmitsJavaNpcSpecificMessages()
	{
		var service = new StorageExpansionNpcService();
		var cubeNpc = CreateNpc(798008, nameId: 123456);
		var warehouseNpc = CreateNpc(203199, nameId: 654321);

		var cube = service.RequestCubeExpansion(
			CreatePlayer(),
			cubeNpc,
			CreateTemplate(level: 3, price: 1000),
			cubeExpansionLimit: 11,
			npcCubeExpandsSizeLimit: 5);
		var warehouse = service.RequestWarehouseExpansion(
			CreatePlayer(),
			warehouseNpc,
			CreateTemplate(level: 4, price: 1200));

		Assert.True(cube.Handled);
		Assert.Equal(StorageExpansionRequestStatus.BelowTemplateMinLevel, cube.Status);
		AssertSystemMessage(
			Assert.Single(cube.Packets),
			1300436,
			ChatUtil.L10n(cubeNpc.Template.NameId),
			"2");
		Assert.True(warehouse.Handled);
		Assert.Equal(StorageExpansionRequestStatus.BelowTemplateMinLevel, warehouse.Status);
		AssertSystemMessage(
			Assert.Single(warehouse.Packets),
			1300438,
			ChatUtil.L10n(warehouseNpc.Template.NameId),
			"3");
	}

	[Fact]
	public void RequestExpansion_AboveNpcMaximumEmitsJavaNpcSpecificMessages()
	{
		var service = new StorageExpansionNpcService();
		var cubePlayer = CreatePlayer();
		cubePlayer.NpcExpands = 5;
		var warehousePlayer = CreatePlayer();
		warehousePlayer.WarehouseNpcExpands = 2;

		var cube = service.RequestCubeExpansion(
			cubePlayer,
			CreateNpc(798008, nameId: 123456),
			CreateTemplate(level: 1, price: 1000),
			cubeExpansionLimit: 11,
			npcCubeExpandsSizeLimit: 5);
		var warehouse = service.RequestWarehouseExpansion(
			warehousePlayer,
			CreateNpc(203199, nameId: 654321),
			CreateTemplate(level: 1, price: 1200));

		Assert.True(cube.Handled);
		Assert.Equal(StorageExpansionRequestStatus.AboveTemplateMaxLevel, cube.Status);
		AssertSystemMessage(
			Assert.Single(cube.Packets),
			1300437,
			ChatUtil.L10n(123456),
			"1");
		Assert.True(warehouse.Handled);
		Assert.Equal(StorageExpansionRequestStatus.AboveTemplateMaxLevel, warehouse.Status);
		AssertSystemMessage(
			Assert.Single(warehouse.Packets),
			1300439,
			ChatUtil.L10n(654321),
			"1");
	}

	[Fact]
	public void HandleResponse_DenyConsumesPendingRequestWithoutMutation()
	{
		var player = CreatePlayer();
		var service = new StorageExpansionNpcService();
		service.RequestCubeExpansion(player, CreateNpc(798008), CreateTemplate(level: 1, price: 1000), 11, 5);

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.WarehouseExpandWarning,
			response: 0,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(StorageExpansionResponseStatus.Denied, result.Status);
		Assert.Equal(0, player.NpcExpands);
		Assert.Equal(10_000, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		Assert.Null(player.PendingStorageExpansionRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_AcceptCubeDecreasesKinahAndExpandsNpcCubeRows()
	{
		var player = CreatePlayer();
		var service = new StorageExpansionNpcService();
		service.RequestCubeExpansion(player, CreateNpc(798008), CreateTemplate(level: 1, price: 1000), 11, 5);

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.WarehouseExpandWarning,
			response: 1,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(StorageExpansionResponseStatus.Accepted, result.Status);
		Assert.Equal(1, player.NpcExpands);
		Assert.Equal(36, InventoryCapacity.GetCubeLimit(player));
		Assert.Equal(9000, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahCube, ReadInventoryUpdateType(result.Packets.OfType<SmInventoryUpdateItem>().Single()));
		Assert.Contains(result.Packets, packet => packet is SmSystemMessage);
		Assert.Contains(result.Packets, packet => packet is SmCubeUpdate);
		Assert.Null(player.PendingStorageExpansionRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_AcceptWarehouseDecreasesKinahAndExpandsNpcWarehouseRows()
	{
		var player = CreatePlayer();
		var service = new StorageExpansionNpcService();
		service.RequestWarehouseExpansion(player, CreateNpc(203199), CreateTemplate(level: 1, price: 1200));

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.WarehouseExpandWarning,
			response: 1,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(StorageExpansionResponseStatus.Accepted, result.Status);
		Assert.Equal(1, player.WarehouseNpcExpands);
		Assert.Equal(32, InventoryCapacity.GetWarehouseLimit(player));
		Assert.Equal(8800, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, ReadInventoryUpdateType(result.Packets.OfType<SmInventoryUpdateItem>().Single()));
		Assert.Contains(result.Packets, packet => packet is SmSystemMessage);
		Assert.Equal(2, result.Packets.Count(packet => packet is SmWarehouseInfo));
	}

	[Fact]
	public void HandleResponse_NotEnoughKinahConsumesPendingRequestWithoutExpansion()
	{
		var player = CreatePlayer(kinah: 100);
		var service = new StorageExpansionNpcService();
		service.RequestCubeExpansion(player, CreateNpc(798008), CreateTemplate(level: 1, price: 1000), 11, 5);

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.WarehouseExpandWarning,
			response: 1,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(StorageExpansionResponseStatus.NotEnoughKinah, result.Status);
		Assert.Equal(0, player.NpcExpands);
		Assert.Equal(100, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		Assert.IsType<SmSystemMessage>(Assert.Single(result.Packets));
		Assert.Null(player.PendingStorageExpansionRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	private static Player CreatePlayer(long kinah = 10_000)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Cube",
			Level = 10,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = 182400001,
					Count = kinah,
					Location = 0,
				},
			],
		};
	}

	private static WorldNpc CreateNpc(int templateId, int objectId = 9001, int nameId = 0)
	{
		return new WorldNpc(
			objectId,
			templateId,
			new NpcTemplateSummary(
				templateId,
				"Expansion Master",
				nameId,
				1,
				"NORMAL",
				"NORMAL",
				"PC_ALL",
				"",
				"NPC",
				FunctionDialogIds: [CmDialogSelect.ExtendInventory],
				HasTalkInfo: true,
				IsDialogNpc: true),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static StorageExpansionTemplateSummary CreateTemplate(int level, int price)
	{
		return new StorageExpansionTemplateSummary([1], [new StorageExpansionPrice(level, price)]);
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(182400001, "Kinah", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
		]);
	}

	private static int ReadInventoryUpdateType(SmInventoryUpdateItem packet)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new Aion.Commons.Network.PacketBuffer(payload);
		reader.ReadD();
		reader.ReadS();
		var blobSize = reader.ReadH();
		reader.ReadB(blobSize);
		return reader.ReadH();
	}

	private static void AssertSystemMessage(GameServerPacket packet, int expectedMessageId, params string[] expectedParameters)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new Aion.Commons.Network.PacketBuffer(payload);
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
