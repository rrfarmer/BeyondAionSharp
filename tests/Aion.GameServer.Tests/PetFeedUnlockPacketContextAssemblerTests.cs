using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedUnlockPacketContextAssemblerTests
{
	[Fact]
	public void Assemble_CubeLocationCreatesCubeContextWithSuppliedSnapshots()
	{
		var assembler = new PetFeedUnlockPacketContextAssembler();
		var item = CreateItem(location: 0, itemId: 188000001);
		var template = CreateTemplate(item.ItemId);

		var result = assembler.Assemble(new PetFeedUnlockPacketContextAssemblerInput(
			item,
			template,
			CubeItemsCount: 7,
			NpcExpands: 2,
			QuestExpands: 1,
			ItemExpands: 3));

		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.Created, result.Status);
		Assert.False(result.IsLive);
		Assert.False(result.IsJavaRuntimeParity);
		Assert.NotNull(result.Context);
		Assert.Equal(PetFeedUnlockPacketStorageKind.Cube, result.Context.StorageKind);
		Assert.Same(item, result.Context.Item);
		Assert.Same(template, result.Context.Template);
		Assert.Equal(7, result.Context.CubeItemsCount);
		Assert.Equal(2, result.Context.NpcExpands);
		Assert.Equal(1, result.Context.QuestExpands);
		Assert.Equal(3, result.Context.ItemExpands);
	}

	[Theory]
	[InlineData(1, PetFeedUnlockPacketStorageKind.Warehouse)]
	[InlineData(2, PetFeedUnlockPacketStorageKind.AccountWarehouse)]
	[InlineData(3, PetFeedUnlockPacketStorageKind.LegionWarehouse)]
	public void Assemble_WarehouseLocationsMapJavaStorageIds(int location, PetFeedUnlockPacketStorageKind expectedKind)
	{
		var assembler = new PetFeedUnlockPacketContextAssembler();
		var item = CreateItem(location, itemId: 188000001);
		var template = CreateTemplate(item.ItemId);

		var result = assembler.Assemble(new PetFeedUnlockPacketContextAssemblerInput(
			item,
			template,
			NpcExpands: 4,
			QuestExpands: 2,
			StorageItemsCount: 11));

		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.Created, result.Status);
		Assert.NotNull(result.Context);
		Assert.Equal(expectedKind, result.Context.StorageKind);
		Assert.Equal(11, result.Context.StorageItemsCount);
		Assert.Equal(4, result.Context.NpcExpands);
		Assert.Equal(2, result.Context.QuestExpands);
		Assert.False(result.Context.IsKinah);
	}

	[Fact]
	public void Assemble_LegionWarehouseKinahDoesNotRequireTemplateLikeJavaSpecialCase()
	{
		var assembler = new PetFeedUnlockPacketContextAssembler();
		var item = CreateItem(location: 3, itemId: 182400001);

		var result = assembler.Assemble(new PetFeedUnlockPacketContextAssemblerInput(
			item,
			Template: null,
			NpcExpands: 6,
			StorageItemsCount: 21,
			LegionWarehouseKinah: 123456));

		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.Created, result.Status);
		Assert.NotNull(result.Context);
		Assert.Equal(PetFeedUnlockPacketStorageKind.LegionWarehouse, result.Context.StorageKind);
		Assert.True(result.Context.IsKinah);
		Assert.Null(result.Context.Template);
		Assert.Equal(123456, result.Context.LegionWarehouseKinah);
	}

	[Fact]
	public void Assemble_MissingTemplateBlocksNonKinahPacketContext()
	{
		var assembler = new PetFeedUnlockPacketContextAssembler();

		var result = assembler.Assemble(new PetFeedUnlockPacketContextAssemblerInput(
			CreateItem(location: 1, itemId: 188000001),
			Template: null));

		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.MissingTemplateSnapshot, result.Status);
		Assert.Null(result.Context);
		Assert.Contains("template", result.Notes);
	}

	[Theory]
	[InlineData(32)]
	[InlineData(33)]
	[InlineData(34)]
	[InlineData(35)]
	[InlineData(36)]
	[InlineData(37)]
	[InlineData(38)]
	[InlineData(39)]
	[InlineData(40)]
	[InlineData(41)]
	[InlineData(42)]
	[InlineData(43)]
	[InlineData(60)]
	[InlineData(61)]
	[InlineData(62)]
	[InlineData(63)]
	[InlineData(64)]
	[InlineData(65)]
	[InlineData(66)]
	[InlineData(67)]
	[InlineData(68)]
	[InlineData(69)]
	[InlineData(70)]
	[InlineData(71)]
	[InlineData(72)]
	[InlineData(73)]
	[InlineData(74)]
	[InlineData(75)]
	[InlineData(76)]
	[InlineData(77)]
	[InlineData(78)]
	[InlineData(79)]
	[InlineData(126)]
	[InlineData(127)]
	public void Assemble_UnsupportedKnownStorageLocationsDoNotGuessPacketShape(int location)
	{
		var assembler = new PetFeedUnlockPacketContextAssembler();

		var result = assembler.Assemble(new PetFeedUnlockPacketContextAssemblerInput(
			CreateItem(location, itemId: 188000001),
			CreateTemplate(188000001)));

		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.UnsupportedStorageLocation, result.Status);
		Assert.Null(result.Context);
	}

	[Fact]
	public void Assemble_UnknownStorageLocationMatchesJavaNoSendBoundary()
	{
		var assembler = new PetFeedUnlockPacketContextAssembler();

		var result = assembler.Assemble(new PetFeedUnlockPacketContextAssemblerInput(
			CreateItem(location: 999, itemId: 188000001),
			CreateTemplate(188000001)));

		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.UnknownStorageLocation, result.Status);
		Assert.Null(result.Context);
		Assert.Contains("sends nothing", result.Notes);
	}

	private static InventoryItem CreateItem(int location, int itemId)
	{
		return new InventoryItem
		{
			ObjectId = 5001,
			ItemId = itemId,
			Count = 2,
			OwnerId = 7001,
			Location = location,
			Slot = 9,
		};
	}

	private static ItemTemplateSummary CreateTemplate(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			0,
			0,
			1,
			string.Empty,
			string.Empty,
			string.Empty,
			string.Empty,
			100,
			0,
			0);
	}
}
