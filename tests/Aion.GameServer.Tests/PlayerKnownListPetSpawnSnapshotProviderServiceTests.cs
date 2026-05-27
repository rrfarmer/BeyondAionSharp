using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListPetSpawnSnapshotProviderServiceTests
{
	[Fact]
	public void Create_WithCompleteSuppliedPetSnapshotBuildsSmPetSpawnSnapshot()
	{
		var service = new PlayerKnownListPetSpawnSnapshotProviderService();

		var result = service.Create(CreateInput());

		Assert.Equal(PlayerKnownListPetSpawnSnapshotProviderStatus.Created, result.Status);
		Assert.False(result.IsLive);
		Assert.False(result.IsJavaPetParity);
		Assert.True(result.MasterIsInFlyingState);
		Assert.True(result.CanCreateFlyStartEmote);
		Assert.Contains("SM_PET(Pet)", result.JavaSource);
		Assert.NotNull(result.Snapshot);
		var snapshot = result.Snapshot!;
		Assert.Equal("Tog", snapshot.Name);
		Assert.Equal(900001, snapshot.TemplateId);
		Assert.Equal(PetObjectId, snapshot.ObjectId);
		Assert.Equal(11.5f, snapshot.X);
		Assert.Equal(22.5f, snapshot.Y);
		Assert.Equal(33.5f, snapshot.Z);
		Assert.Equal(44.5f, snapshot.TargetX);
		Assert.Equal(55.5f, snapshot.TargetY);
		Assert.Equal(66.5f, snapshot.TargetZ);
		Assert.Equal(90, snapshot.Heading);
		Assert.Equal(MasterPlayerObjectId, snapshot.MasterObjectId);
		Assert.Equal(12345, snapshot.Decoration);
	}

	[Fact]
	public void Create_UsesJavaFlyingStateNotGenericGlidingForFlyStartMetadata()
	{
		var service = new PlayerKnownListPetSpawnSnapshotProviderService();

		var result = service.Create(CreateInput(masterIsInFlyingState: false));

		Assert.Equal(PlayerKnownListPetSpawnSnapshotProviderStatus.Created, result.Status);
		Assert.False(result.MasterIsInFlyingState);
		Assert.False(result.CanCreateFlyStartEmote);
		Assert.NotNull(result.Snapshot);
	}

	[Theory]
	[InlineData("pet", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingActivePet)]
	[InlineData("name", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetName)]
	[InlineData("template", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetTemplate)]
	[InlineData("position", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetPosition)]
	[InlineData("move", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetMoveTarget)]
	[InlineData("heading", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetHeading)]
	[InlineData("master", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetMaster)]
	[InlineData("common", PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetCommonData)]
	public void Create_MissingJavaRequiredFieldBlocksSnapshot(string missing, PlayerKnownListPetSpawnSnapshotProviderStatus expected)
	{
		var service = new PlayerKnownListPetSpawnSnapshotProviderService();

		var result = service.Create(CreateInput(missing: missing));

		Assert.Equal(expected, result.Status);
		Assert.Null(result.Snapshot);
		Assert.False(result.CanCreateFlyStartEmote);
		Assert.Contains("Java", result.Notes);
	}

	[Fact]
	public void TryCreate_ReturnsFalseAndNullOutputForBlockedSnapshot()
	{
		var service = new PlayerKnownListPetSpawnSnapshotProviderService();

		var created = service.TryCreate(CreateInput(missing: "pet"), out var snapshot);

		Assert.False(created);
		Assert.Null(snapshot);
	}

	private static PlayerKnownListPetSpawnSnapshotProviderInput CreateInput(
		bool masterIsInFlyingState = true,
		string? missing = null) =>
		new(
			MasterPlayerObjectId,
			PetObjectId: missing == "pet" ? null : PetObjectId,
			PetName: missing == "name" ? null : "Tog",
			PetTemplateId: missing == "template" ? null : 900001,
			X: missing == "position" ? null : 11.5f,
			Y: missing == "position" ? null : 22.5f,
			Z: missing == "position" ? null : 33.5f,
			TargetX: missing == "move" ? null : 44.5f,
			TargetY: missing == "move" ? null : 55.5f,
			TargetZ: missing == "move" ? null : 66.5f,
			Heading: missing == "heading" ? null : (byte)90,
			PetMasterObjectId: missing == "master" ? null : MasterPlayerObjectId,
			CommonDataDecoration: missing == "common" ? null : 12345,
			masterIsInFlyingState);

	private const int MasterPlayerObjectId = 9002;
	private const int PetObjectId = 9102;
}
