using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPetVisibilityPacketConstructionServiceTests
{
	[Fact]
	public void Construct_SeeWithFlyingMasterBuildsSmPetThenFlyStartEmote()
	{
		var plan = CreatePetVisibilityPlan(PlayerKnownListPetVisibilityTransition.See, masterIsFlying: true);
		var service = new PlayerKnownListPetVisibilityPacketConstructionService();

		var construction = service.Construct(new PlayerKnownListPetVisibilityPacketConstructionRequest(
			plan,
			CreateSpawnSnapshot()));

		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionStatus.Constructed, construction.Status);
		Assert.False(construction.ExecutesLivePackets);
		Assert.False(construction.IsLive);
		Assert.False(construction.IsJavaControllerParity);
		Assert.Equal(
			[
				PlayerKnownListPetVisibilitySideEffectKind.SmPetSpawn,
				PlayerKnownListPetVisibilitySideEffectKind.SmPetEmoteFlyStart,
			],
			construction.Results.Select(result => result.Descriptor.Kind));
		var spawn = Assert.IsType<SmPet>(construction.Results[0].Packet);
		var flyStart = Assert.IsType<SmPetEmote>(construction.Results[1].Packet);
		Assert.Equal(SmPet.PacketOpCode, spawn.OpCode);
		Assert.Equal(SmPetEmote.PacketOpCode, flyStart.OpCode);
		Assert.All(construction.Results, result => Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed, result.Status));
	}

	[Fact]
	public void Construct_NotSeeBuildsSmPetDismissWithAnimation()
	{
		var plan = CreatePetVisibilityPlan(PlayerKnownListPetVisibilityTransition.NotSee);
		var service = new PlayerKnownListPetVisibilityPacketConstructionService();

		var construction = service.Construct(new PlayerKnownListPetVisibilityPacketConstructionRequest(plan));

		var result = Assert.Single(construction.Results);
		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionStatus.Constructed, construction.Status);
		Assert.Equal(PlayerKnownListPetVisibilitySideEffectKind.SmPetDismiss, result.Descriptor.Kind);
		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed, result.Status);
		var packet = Assert.IsType<SmPet>(result.Packet);
		Assert.Equal(SmPet.PacketOpCode, packet.OpCode);
	}

	[Fact]
	public void Construct_MissingSpawnSnapshotBlocksOnlySpawnDescriptor()
	{
		var plan = CreatePetVisibilityPlan(PlayerKnownListPetVisibilityTransition.See, masterIsFlying: true);
		var service = new PlayerKnownListPetVisibilityPacketConstructionService();

		var construction = service.Construct(new PlayerKnownListPetVisibilityPacketConstructionRequest(plan));

		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionStatus.PartiallyConstructed, construction.Status);
		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionResultStatus.BlockedMissingSpawnSnapshot, construction.Results[0].Status);
		Assert.Null(construction.Results[0].Packet);
		Assert.Contains("Pet/PetCommonData", construction.Results[0].Notes);
		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed, construction.Results[1].Status);
		Assert.IsType<SmPetEmote>(construction.Results[1].Packet);
	}

	[Fact]
	public void Construct_SpawnSnapshotPetMismatchBlocksSpawn()
	{
		var plan = CreatePetVisibilityPlan(PlayerKnownListPetVisibilityTransition.See);
		var service = new PlayerKnownListPetVisibilityPacketConstructionService();

		var construction = service.Construct(new PlayerKnownListPetVisibilityPacketConstructionRequest(
			plan,
			CreateSpawnSnapshot(objectId: 9999)));

		var result = Assert.Single(construction.Results);
		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionStatus.PartiallyConstructed, construction.Status);
		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionResultStatus.BlockedPetObjectMismatch, result.Status);
		Assert.Null(result.Packet);
		Assert.Contains("does not match", result.Notes);
	}

	[Fact]
	public void Construct_NoDescriptorsReportsNoDescriptors()
	{
		var plan = CreatePetVisibilityPlan(PlayerKnownListPetVisibilityTransition.See, masterHasPet: false);
		var service = new PlayerKnownListPetVisibilityPacketConstructionService();

		var construction = service.Construct(new PlayerKnownListPetVisibilityPacketConstructionRequest(plan));

		Assert.Equal(PlayerKnownListPetVisibilityPacketConstructionStatus.NoDescriptors, construction.Status);
		Assert.Empty(construction.Results);
	}

	private static PlayerKnownListPetVisibilityOrderPlan CreatePetVisibilityPlan(
		PlayerKnownListPetVisibilityTransition transition,
		bool masterIsFlying = false,
		bool masterHasPet = true)
	{
		var service = new PlayerKnownListPetVisibilityOrderPlanService();
		return service.Plan(new PlayerKnownListPetVisibilityOrderRequest(
			transition,
			ViewerPlayerObjectId,
			MasterPlayerObjectId,
			masterHasPet ? PetObjectId : null,
			MasterHasPet: masterHasPet,
			PetAlreadyKnownByViewer: true,
			MasterVisibleToViewer: true,
			MasterIsFlying: masterIsFlying,
			NotSeeAnimation: ObjectDeleteAnimation.JumpIn));
	}

	private static SmPetSpawnSnapshot CreateSpawnSnapshot(int objectId = PetObjectId) =>
		new(
			Name: "Tog",
			TemplateId: 900001,
			ObjectId: objectId,
			X: 11,
			Y: 22,
			Z: 33,
			TargetX: 44,
			TargetY: 55,
			TargetZ: 66,
			Heading: 90,
			MasterObjectId: MasterPlayerObjectId,
			Decoration: 12345);

	private const int ViewerPlayerObjectId = 9001;
	private const int MasterPlayerObjectId = 9002;
	private const int PetObjectId = 9102;
}
