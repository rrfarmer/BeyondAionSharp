using Aion.GameServer.Model;

namespace Aion.GameServer.Services;

public enum PlayerKnownListPetVisibilityOrderPlanStatus
{
	NoPet,
	Planned,
	SkippedViewerNotSpawned,
}

public enum PlayerKnownListPetVisibilityTransition
{
	See,
	NotSee,
}

public enum PlayerKnownListPetVisibilitySideEffectKind
{
	SmPetSpawn,
	SmPetEmoteFlyStart,
	SmPetDismiss,
}

public enum PlayerKnownListPetVisibilityOrdering
{
	AfterMasterPlayerVisibilityCallback,
}

public sealed record PlayerKnownListPetVisibilityOrderRequest(
	PlayerKnownListPetVisibilityTransition Transition,
	int ViewerPlayerObjectId,
	int MasterPlayerObjectId,
	int? PetObjectId,
	bool MasterHasPet,
	bool PetAlreadyKnownByViewer,
	bool MasterVisibleToViewer,
	bool MasterIsFlying = false,
	bool ViewerIsSpawned = true,
	ObjectDeleteAnimation NotSeeAnimation = ObjectDeleteAnimation.FadeOut);

public sealed record PlayerKnownListPetVisibilitySideEffectDescriptor(
	PlayerKnownListPetVisibilitySideEffectKind Kind,
	string JavaPacketName,
	string? CSharpPacketTypeName,
	PlayerKnownListPlayerSideEffectCSharpSupport CSharpSupport,
	int ViewerPlayerObjectId,
	int MasterPlayerObjectId,
	int PetObjectId,
	ObjectDeleteAnimation? DeleteAnimation,
	string JavaSource,
	string Notes);

public sealed record PlayerKnownListPetVisibilityOrderPlan(
	PlayerKnownListPetVisibilityOrderPlanStatus Status,
	PlayerKnownListPetVisibilityTransition Transition,
	int ViewerPlayerObjectId,
	int MasterPlayerObjectId,
	int? PetObjectId,
	PlayerKnownListPetVisibilityOrdering Ordering,
	IReadOnlyList<PlayerKnownListPetVisibilitySideEffectDescriptor> Descriptors,
	bool RequiresMasterVisibilityBeforePetVisibility,
	bool RepairsPetKnownBeforeMasterVisible,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaKnownListParity,
	string JavaSource,
	string Notes);

public sealed class PlayerKnownListPetVisibilityOrderPlanService
{
	public PlayerKnownListPetVisibilityOrderPlan Plan(PlayerKnownListPetVisibilityOrderRequest request)
	{
		// Java parity breadcrumb: KnownList.updateVisibility calls notifySee or
		// notifyNotSee for the player, then updatePetVisibility(knownObject).
		// PlayerController.see(Pet) writes SM_PET and maybe SM_PET_EMOTE(FLY_START).
		const string javaSource =
			"com.aionemu.gameserver.world.knownlist.KnownList.updateVisibility/updatePetVisibility; "
			+ "com.aionemu.gameserver.controllers.PlayerController.see/notSee(Pet)";

		if (!request.MasterHasPet || request.PetObjectId is null)
		{
			return CreatePlan(
				PlayerKnownListPetVisibilityOrderPlanStatus.NoPet,
				request,
				[],
				javaSource,
				"No dependent pet visibility side effect is planned because the supplied master snapshot has no pet.");
		}

		if (request.Transition == PlayerKnownListPetVisibilityTransition.NotSee && !request.ViewerIsSpawned)
		{
			return CreatePlan(
				PlayerKnownListPetVisibilityOrderPlanStatus.SkippedViewerNotSpawned,
				request,
				[],
				javaSource,
				"Java PlayerController.notSee(Pet) skips deletion packets while the viewer is unspawned or teleporting.");
		}

		if (request.Transition == PlayerKnownListPetVisibilityTransition.See && !request.MasterVisibleToViewer)
		{
			return CreatePlan(
				PlayerKnownListPetVisibilityOrderPlanStatus.NoPet,
				request,
				[],
				javaSource,
				"Pet spawn is not planned until the master is visible to the viewer; Java Creature.canSee gates pet visibility through master visibility.");
		}

		var descriptors = request.Transition == PlayerKnownListPetVisibilityTransition.See
			? CreateSeeDescriptors(request, javaSource)
			: CreateNotSeeDescriptors(request, javaSource);

		return CreatePlan(
			PlayerKnownListPetVisibilityOrderPlanStatus.Planned,
			request,
			descriptors,
			javaSource,
			"Dependent pet visibility side effects are ordered after the master player visibility callback. C# pet packet serializers are not ported yet, so descriptors are metadata only.");
	}

	private static IReadOnlyList<PlayerKnownListPetVisibilitySideEffectDescriptor> CreateSeeDescriptors(
		PlayerKnownListPetVisibilityOrderRequest request,
		string javaSource)
	{
		var descriptors = new List<PlayerKnownListPetVisibilitySideEffectDescriptor>
		{
			new(
				PlayerKnownListPetVisibilitySideEffectKind.SmPetSpawn,
				"SM_PET",
				CSharpPacketTypeName: null,
				PlayerKnownListPlayerSideEffectCSharpSupport.Missing,
				request.ViewerPlayerObjectId,
				request.MasterPlayerObjectId,
				request.PetObjectId!.Value,
				DeleteAnimation: null,
				javaSource,
				"Java PlayerController.see(Pet) sends new SM_PET(pet). C# serializer and pet object/common-data model are not ported."),
		};

		if (request.MasterIsFlying)
		{
			descriptors.Add(new PlayerKnownListPetVisibilitySideEffectDescriptor(
				PlayerKnownListPetVisibilitySideEffectKind.SmPetEmoteFlyStart,
				"SM_PET_EMOTE",
				CSharpPacketTypeName: null,
				PlayerKnownListPlayerSideEffectCSharpSupport.Missing,
				request.ViewerPlayerObjectId,
				request.MasterPlayerObjectId,
				request.PetObjectId!.Value,
				DeleteAnimation: null,
				javaSource,
				"Java PlayerController.see(Pet) sends SM_PET_EMOTE(pet, PetEmote.FLY_START) when the pet master is flying. C# PetEmote and packet serializer are not ported."));
		}

		return descriptors;
	}

	private static IReadOnlyList<PlayerKnownListPetVisibilitySideEffectDescriptor> CreateNotSeeDescriptors(
		PlayerKnownListPetVisibilityOrderRequest request,
		string javaSource) =>
		[
			new(
				PlayerKnownListPetVisibilitySideEffectKind.SmPetDismiss,
				"SM_PET",
				CSharpPacketTypeName: null,
				PlayerKnownListPlayerSideEffectCSharpSupport.Missing,
				request.ViewerPlayerObjectId,
				request.MasterPlayerObjectId,
				request.PetObjectId!.Value,
				request.NotSeeAnimation,
				javaSource,
				"Java PlayerController.notSee(Pet) sends new SM_PET(petObjectId, animation), not SM_DELETE. C# dismiss serializer is not ported."),
		];

	private static PlayerKnownListPetVisibilityOrderPlan CreatePlan(
		PlayerKnownListPetVisibilityOrderPlanStatus status,
		PlayerKnownListPetVisibilityOrderRequest request,
		IReadOnlyList<PlayerKnownListPetVisibilitySideEffectDescriptor> descriptors,
		string javaSource,
		string notes) =>
		new(
			status,
			request.Transition,
			request.ViewerPlayerObjectId,
			request.MasterPlayerObjectId,
			request.PetObjectId,
			PlayerKnownListPetVisibilityOrdering.AfterMasterPlayerVisibilityCallback,
			descriptors,
			RequiresMasterVisibilityBeforePetVisibility: true,
			RepairsPetKnownBeforeMasterVisible: request.PetAlreadyKnownByViewer,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaKnownListParity: false,
			javaSource,
			notes);
}
