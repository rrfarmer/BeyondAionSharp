using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPetVisibilityOrderPlanServiceTests
{
	[Fact]
	public void Plan_SeePetSpawnAfterMasterPlayerVisibilityCallback()
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();

		var plan = service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			PlayerKnownListPetVisibilityTransition.See,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			PetObjectId,
			MasterHasPet: true,
			PetAlreadyKnownByViewer: true,
			MasterVisibleToViewer: true));

		Assert.Equal(PlayerKnownListPetVisibilityOrderPlanStatus.Planned, plan.Status);
		Assert.Equal(PlayerKnownListPetVisibilityOrdering.AfterMasterPlayerVisibilityCallback, plan.Ordering);
		Assert.True(plan.RequiresMasterVisibilityBeforePetVisibility);
		Assert.True(plan.RepairsPetKnownBeforeMasterVisible);
		Assert.False(plan.ExecutesLivePackets);
		Assert.False(plan.IsLive);
		Assert.False(plan.IsJavaKnownListParity);
		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(PlayerKnownListPetVisibilitySideEffectKind.SmPetSpawn, descriptor.Kind);
		Assert.Equal("SM_PET", descriptor.JavaPacketName);
		Assert.Equal(nameof(SmPet), descriptor.CSharpPacketTypeName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Partial, descriptor.CSharpSupport);
		Assert.Equal(PetObjectId, descriptor.PetObjectId);
		Assert.Contains("updatePetVisibility", descriptor.JavaSource);
		Assert.Contains("serializer subset", descriptor.Notes);
	}

	[Fact]
	public void Plan_FlyingMasterAddsFlyStartEmoteAfterPetSpawn()
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();

		var plan = service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			PlayerKnownListPetVisibilityTransition.See,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			PetObjectId,
			MasterHasPet: true,
			PetAlreadyKnownByViewer: false,
			MasterVisibleToViewer: true,
			MasterIsFlying: true));

		Assert.Equal(
			[
				PlayerKnownListPetVisibilitySideEffectKind.SmPetSpawn,
				PlayerKnownListPetVisibilitySideEffectKind.SmPetEmoteFlyStart,
			],
			plan.Descriptors.Select(descriptor => descriptor.Kind));
		Assert.Equal("SM_PET_EMOTE", plan.Descriptors[1].JavaPacketName);
		Assert.Equal(nameof(SmPetEmote), plan.Descriptors[1].CSharpPacketTypeName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Partial, plan.Descriptors[1].CSharpSupport);
		Assert.Contains("FLY_START", plan.Descriptors[1].Notes);
	}

	[Fact]
	public void Plan_NotSeePetUsesSmPetDismissPacketInsteadOfDelete()
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();

		var plan = service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			PlayerKnownListPetVisibilityTransition.NotSee,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			PetObjectId,
			MasterHasPet: true,
			PetAlreadyKnownByViewer: true,
			MasterVisibleToViewer: false,
			NotSeeAnimation: ObjectDeleteAnimation.FadeOut));

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(PlayerKnownListPetVisibilitySideEffectKind.SmPetDismiss, descriptor.Kind);
		Assert.Equal("SM_PET", descriptor.JavaPacketName);
		Assert.Equal(ObjectDeleteAnimation.FadeOut, descriptor.DeleteAnimation);
		Assert.Equal(nameof(SmPet), descriptor.CSharpPacketTypeName);
		Assert.Equal(PlayerKnownListPlayerSideEffectCSharpSupport.Partial, descriptor.CSharpSupport);
		Assert.Contains("not SM_DELETE", descriptor.Notes);
	}

	[Fact]
	public void Plan_SeeDoesNotSpawnPetBeforeMasterVisible()
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();

		var plan = service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			PlayerKnownListPetVisibilityTransition.See,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			PetObjectId,
			MasterHasPet: true,
			PetAlreadyKnownByViewer: true,
			MasterVisibleToViewer: false));

		Assert.Equal(PlayerKnownListPetVisibilityOrderPlanStatus.NoPet, plan.Status);
		Assert.Empty(plan.Descriptors);
		Assert.True(plan.RequiresMasterVisibilityBeforePetVisibility);
		Assert.True(plan.RepairsPetKnownBeforeMasterVisible);
		Assert.Contains("master is visible", plan.Notes);
	}

	[Fact]
	public void Plan_NotSeeSkipsDismissWhenViewerIsUnspawned()
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();

		var plan = service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			PlayerKnownListPetVisibilityTransition.NotSee,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			PetObjectId,
			MasterHasPet: true,
			PetAlreadyKnownByViewer: true,
			MasterVisibleToViewer: false,
			ViewerIsSpawned: false));

		Assert.Equal(PlayerKnownListPetVisibilityOrderPlanStatus.SkippedViewerNotSpawned, plan.Status);
		Assert.Empty(plan.Descriptors);
		Assert.Contains("unspawned", plan.Notes);
	}

	[Fact]
	public void Plan_NoPetWhenMasterSnapshotHasNoPet()
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();

		var plan = service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			PlayerKnownListPetVisibilityTransition.See,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			PetObjectId: null,
			MasterHasPet: false,
			PetAlreadyKnownByViewer: false,
			MasterVisibleToViewer: true));

		Assert.Equal(PlayerKnownListPetVisibilityOrderPlanStatus.NoPet, plan.Status);
		Assert.Null(plan.PetObjectId);
		Assert.Empty(plan.Descriptors);
	}

	private const int ViewerPlayerObjectId = 9001;
	private const int MasterPlayerObjectId = 9002;
	private const int PetObjectId = 9102;
}
