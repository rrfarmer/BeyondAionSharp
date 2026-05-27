using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services.ToyPet;

public enum PetFeedPacketMetadataBridgeStatus
{
	NoOperations,
	Constructed,
	PartiallyConstructed,
	Blocked,
}

public enum PetFeedPacketMetadataResultStatus
{
	Constructed,
	SkippedNonPacketOperation,
	BlockedItemUnlockPacket,
	BlockedEmotionContext,
	BlockedSystemMessageContext,
}

public sealed record PetFeedPacketMetadataBridgeRequest(
	PetFeedServiceOperationPlan Plan,
	int FeedProgressData,
	int RefeedDelaySeconds = 0);

public sealed record PetFeedPacketMetadataResult(
	PetFeedServiceOperation Operation,
	PetFeedPacketMetadataResultStatus Status,
	SmPet? Packet,
	string Notes);

public sealed record PetFeedPacketMetadataBridgeResult(
	PetFeedPacketMetadataBridgeStatus Status,
	IReadOnlyList<PetFeedPacketMetadataResult> Results,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaRuntimeParity);

public sealed class PetFeedPacketMetadataBridge
{
	public PetFeedPacketMetadataBridgeResult Construct(PetFeedPacketMetadataBridgeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(request.Plan);

		if (request.Plan.Operations.Count == 0)
		{
			return new PetFeedPacketMetadataBridgeResult(
				PetFeedPacketMetadataBridgeStatus.NoOperations,
				Results: [],
				ExecutesLivePackets: false,
				IsLive: false,
				IsJavaRuntimeParity: false);
		}

		var results = request.Plan.Operations
			.Select(operation => Construct(operation, request.FeedProgressData, request.RefeedDelaySeconds))
			.ToArray();

		var constructed = results.Count(result => result.Status == PetFeedPacketMetadataResultStatus.Constructed);
		var blocked = results.Length - constructed - results.Count(result => result.Status == PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation);
		var status = constructed switch
		{
			0 when blocked == 0 => PetFeedPacketMetadataBridgeStatus.NoOperations,
			0 => PetFeedPacketMetadataBridgeStatus.Blocked,
			_ when blocked > 0 => PetFeedPacketMetadataBridgeStatus.PartiallyConstructed,
			_ => PetFeedPacketMetadataBridgeStatus.Constructed,
		};

		return new PetFeedPacketMetadataBridgeResult(
			status,
			results,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaRuntimeParity: false);
	}

	private static PetFeedPacketMetadataResult Construct(
		PetFeedServiceOperation operation,
		int feedProgressData,
		int refeedDelaySeconds)
	{
		return operation.Kind switch
		{
			PetFeedServiceOperationKind.SendPetFeedProgressPacket => ConstructSmPet(
				operation,
				subType: 2,
				feedProgressData,
				itemObjectId: operation.ItemObjectId ?? 0,
				count: operation.Count ?? 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.SendPetFeedEndPacket => ConstructSmPet(
				operation,
				subType: 5,
				feedProgressData,
				itemObjectId: 0,
				count: 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.SendPetRewardItemPacket => ConstructSmPet(
				operation,
				subType: 6,
				feedProgressData,
				itemObjectId: operation.ItemId ?? 0,
				count: 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.SendPetRefeedPacket => ConstructSmPet(
				operation,
				subType: 7,
				feedProgressData,
				itemObjectId: 0,
				count: 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.UnlockFoodItem => Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				"Item unlock packet/service boundary is not represented by SmPet."),
			PetFeedServiceOperationKind.SendEndFeedingEmotion => Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedEmotionContext,
				"SM_EMOTION EndFeeding needs a live/supplied player context and is not constructed by this bridge."),
			PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage => Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedSystemMessageContext,
				"Rejected-food system message needs pet name and localized item name context."),
			_ => new PetFeedPacketMetadataResult(
				operation,
				PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation,
				Packet: null,
				Notes: "Operation is not a packet-construction boundary."),
		};
	}

	private static PetFeedPacketMetadataResult ConstructSmPet(
		PetFeedServiceOperation operation,
		int subType,
		int feedProgressData,
		int itemObjectId,
		int count,
		int refeedDelaySeconds)
	{
		// Java parity: network/aion/serverpackets/SM_PET FOOD subtypes are constructed but never sent here.
		return new PetFeedPacketMetadataResult(
			operation,
			PetFeedPacketMetadataResultStatus.Constructed,
			SmPet.Food(new SmPetFoodSnapshot(subType, feedProgressData, itemObjectId, count, refeedDelaySeconds)),
			Notes: $"Constructed non-sending SmPet FOOD subtype {subType} metadata.");
	}

	private static PetFeedPacketMetadataResult Blocked(
		PetFeedServiceOperation operation,
		PetFeedPacketMetadataResultStatus status,
		string notes)
	{
		return new PetFeedPacketMetadataResult(operation, status, Packet: null, notes);
	}
}
