using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services.ToyPet;

public enum PetFeedUnlockPacketContextAssemblerStatus
{
	Created,
	MissingItemSnapshot,
	MissingTemplateSnapshot,
	UnknownStorageLocation,
	UnsupportedStorageLocation,
}

public sealed record PetFeedUnlockPacketContextAssemblerInput(
	InventoryItem? Item,
	ItemTemplateSummary? Template,
	int CubeItemsCount = 0,
	int NpcExpands = 0,
	int QuestExpands = 0,
	int ItemExpands = 0,
	int StorageItemsCount = 0,
	long LegionWarehouseKinah = 0);

public sealed record PetFeedUnlockPacketContextAssemblerResult(
	PetFeedUnlockPacketContextAssemblerStatus Status,
	PetFeedUnlockPacketContext? Context,
	bool IsLive,
	bool IsJavaRuntimeParity,
	string Notes);

public sealed class PetFeedUnlockPacketContextAssembler
{
	private const int CubeStorageId = 0;
	private const int RegularWarehouseStorageId = 1;
	private const int AccountWarehouseStorageId = 2;
	private const int LegionWarehouseStorageId = 3;
	private const int KinahItemId = 182400001;

	public PetFeedUnlockPacketContextAssemblerResult Assemble(PetFeedUnlockPacketContextAssemblerInput input)
	{
		ArgumentNullException.ThrowIfNull(input);

		if (input.Item is null)
		{
			return Blocked(
				PetFeedUnlockPacketContextAssemblerStatus.MissingItemSnapshot,
				"Java sendItemUnlockPacket reads item location/object/template state; supplied item snapshot is required.");
		}

		var storageKind = GetStorageKind(input.Item.Location);
		if (storageKind is null)
		{
			return Blocked(
				PetFeedUnlockPacketContextAssemblerStatus.UnknownStorageLocation,
				$"Java StorageType.getStorageTypeById({input.Item.Location}) returns null; sendItemUnlockPacket sends nothing.");
		}

		if (storageKind == PetFeedUnlockPacketStorageKind.Unknown)
		{
			return Blocked(
				PetFeedUnlockPacketContextAssemblerStatus.UnsupportedStorageLocation,
				$"Storage location {input.Item.Location} resolves outside the modeled cube/warehouse unlock packet families.");
		}

		var isKinah = storageKind == PetFeedUnlockPacketStorageKind.LegionWarehouse && input.Item.ItemId == KinahItemId;
		if (!isKinah && input.Template is null)
		{
			return Blocked(
				PetFeedUnlockPacketContextAssemblerStatus.MissingTemplateSnapshot,
				"Storage unlock packet metadata needs supplied item-template snapshot for non-kinah item serialization.");
		}

		// Java parity: ItemPacketService.sendItemUnlockPacket maps item.getItemLocation() through
		// StorageType.getStorageTypeById, then sends ItemAddType.ALL_SLOT storage update packets.
		var context = new PetFeedUnlockPacketContext(
			storageKind.Value,
			input.Item,
			input.Template,
			input.CubeItemsCount,
			input.NpcExpands,
			input.QuestExpands,
			input.ItemExpands,
			input.StorageItemsCount,
			isKinah,
			input.LegionWarehouseKinah);
		return new PetFeedUnlockPacketContextAssemblerResult(
			PetFeedUnlockPacketContextAssemblerStatus.Created,
			context,
			IsLive: false,
			IsJavaRuntimeParity: false,
			Notes: $"Created non-live unlock packet context for storage location {input.Item.Location}.");
	}

	private static PetFeedUnlockPacketStorageKind? GetStorageKind(int location)
	{
		return location switch
		{
			CubeStorageId => PetFeedUnlockPacketStorageKind.Cube,
			RegularWarehouseStorageId => PetFeedUnlockPacketStorageKind.Warehouse,
			AccountWarehouseStorageId => PetFeedUnlockPacketStorageKind.AccountWarehouse,
			LegionWarehouseStorageId => PetFeedUnlockPacketStorageKind.LegionWarehouse,
			>= 32 and <= 43 => PetFeedUnlockPacketStorageKind.Unknown,
			>= 60 and <= 79 => PetFeedUnlockPacketStorageKind.Unknown,
			126 or 127 => PetFeedUnlockPacketStorageKind.Unknown,
			_ => null,
		};
	}

	private static PetFeedUnlockPacketContextAssemblerResult Blocked(
		PetFeedUnlockPacketContextAssemblerStatus status,
		string notes)
	{
		return new PetFeedUnlockPacketContextAssemblerResult(
			status,
			Context: null,
			IsLive: false,
			IsJavaRuntimeParity: false,
			notes);
	}
}
