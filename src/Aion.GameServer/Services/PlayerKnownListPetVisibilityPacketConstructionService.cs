using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerKnownListPetVisibilityPacketConstructionStatus
{
	Constructed,
	PartiallyConstructed,
	NoDescriptors,
}

public enum PlayerKnownListPetVisibilityPacketConstructionResultStatus
{
	Constructed,
	BlockedMissingSpawnSnapshot,
	BlockedPetObjectMismatch,
	BlockedMissingDeleteAnimation,
	UnsupportedDescriptor,
}

public sealed record PlayerKnownListPetVisibilityPacketConstructionRequest(
	PlayerKnownListPetVisibilityOrderPlan PetVisibilityPlan,
	SmPetSpawnSnapshot? SpawnSnapshot = null);

public sealed record PlayerKnownListPetVisibilityPacketConstructionResult(
	PlayerKnownListPetVisibilitySideEffectDescriptor Descriptor,
	PlayerKnownListPetVisibilityPacketConstructionResultStatus Status,
	GameServerPacket? Packet,
	string Notes = "");

public sealed record PlayerKnownListPetVisibilityPacketConstructionPlan(
	PlayerKnownListPetVisibilityOrderPlan PetVisibilityPlan,
	IReadOnlyList<PlayerKnownListPetVisibilityPacketConstructionResult> Results,
	PlayerKnownListPetVisibilityPacketConstructionStatus Status,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaControllerParity,
	string JavaSource);

public sealed class PlayerKnownListPetVisibilityPacketConstructionService
{
	public PlayerKnownListPetVisibilityPacketConstructionPlan Construct(
		PlayerKnownListPetVisibilityPacketConstructionRequest request)
	{
		// Java parity breadcrumb: PlayerController.see(Pet) and notSee(Pet)
		// construct SM_PET/SM_PET_EMOTE after KnownList.updatePetVisibility.
		// This bridge constructs packet objects only from supplied snapshots; it never sends.
		var results = request.PetVisibilityPlan.Descriptors
			.Select(descriptor => ConstructDescriptor(request, descriptor))
			.ToArray();
		var status = results.Length == 0
			? PlayerKnownListPetVisibilityPacketConstructionStatus.NoDescriptors
			: results.All(result => result.Status == PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed)
				? PlayerKnownListPetVisibilityPacketConstructionStatus.Constructed
				: PlayerKnownListPetVisibilityPacketConstructionStatus.PartiallyConstructed;

		return new PlayerKnownListPetVisibilityPacketConstructionPlan(
			request.PetVisibilityPlan,
			results,
			status,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Non-live pet packet construction metadata for com.aionemu.gameserver.controllers.PlayerController see/notSee(Pet).");
	}

	private static PlayerKnownListPetVisibilityPacketConstructionResult ConstructDescriptor(
		PlayerKnownListPetVisibilityPacketConstructionRequest request,
		PlayerKnownListPetVisibilitySideEffectDescriptor descriptor) =>
		descriptor.Kind switch
		{
			PlayerKnownListPetVisibilitySideEffectKind.SmPetSpawn => ConstructSpawn(request, descriptor),
			PlayerKnownListPetVisibilitySideEffectKind.SmPetEmoteFlyStart => Constructed(
				descriptor,
				new SmPetEmote(new SmPetEmoteSnapshot(descriptor.PetObjectId, Model.GameObjects.PetEmote.FlyStart))),
			PlayerKnownListPetVisibilitySideEffectKind.SmPetDismiss => ConstructDismiss(descriptor),
			_ => Blocked(
				descriptor,
				PlayerKnownListPetVisibilityPacketConstructionResultStatus.UnsupportedDescriptor,
				"Pet visibility descriptor kind is not supported by the non-live construction bridge."),
		};

	private static PlayerKnownListPetVisibilityPacketConstructionResult ConstructSpawn(
		PlayerKnownListPetVisibilityPacketConstructionRequest request,
		PlayerKnownListPetVisibilitySideEffectDescriptor descriptor)
	{
		if (request.SpawnSnapshot is null)
		{
			return Blocked(
				descriptor,
				PlayerKnownListPetVisibilityPacketConstructionResultStatus.BlockedMissingSpawnSnapshot,
				"Java reads Pet/PetCommonData/move-controller fields; no supplied pet spawn snapshot was provided.");
		}

		if (request.SpawnSnapshot.ObjectId != descriptor.PetObjectId)
		{
			return Blocked(
				descriptor,
				PlayerKnownListPetVisibilityPacketConstructionResultStatus.BlockedPetObjectMismatch,
				"Supplied pet spawn snapshot object id does not match the visibility descriptor pet id.");
		}

		return Constructed(descriptor, new SmPet(request.SpawnSnapshot));
	}

	private static PlayerKnownListPetVisibilityPacketConstructionResult ConstructDismiss(
		PlayerKnownListPetVisibilitySideEffectDescriptor descriptor)
	{
		if (descriptor.DeleteAnimation is null)
		{
			return Blocked(
				descriptor,
				PlayerKnownListPetVisibilityPacketConstructionResultStatus.BlockedMissingDeleteAnimation,
				"Java SM_PET dismiss requires ObjectDeleteAnimation; descriptor has no animation.");
		}

		return Constructed(descriptor, new SmPet(descriptor.PetObjectId, descriptor.DeleteAnimation.Value));
	}

	private static PlayerKnownListPetVisibilityPacketConstructionResult Constructed(
		PlayerKnownListPetVisibilitySideEffectDescriptor descriptor,
		GameServerPacket packet) =>
		new(
			descriptor,
			PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed,
			packet);

	private static PlayerKnownListPetVisibilityPacketConstructionResult Blocked(
		PlayerKnownListPetVisibilitySideEffectDescriptor descriptor,
		PlayerKnownListPetVisibilityPacketConstructionResultStatus status,
		string notes) =>
		new(descriptor, status, Packet: null, notes);
}
