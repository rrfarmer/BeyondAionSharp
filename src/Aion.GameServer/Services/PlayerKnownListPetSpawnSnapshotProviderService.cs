using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerKnownListPetSpawnSnapshotProviderStatus
{
	Created,
	MissingActivePet,
	MissingPetName,
	MissingPetTemplate,
	MissingPetPosition,
	MissingPetMoveTarget,
	MissingPetHeading,
	MissingPetMaster,
	MissingPetCommonData,
}

public sealed record PlayerKnownListPetSpawnSnapshotProviderInput(
	int MasterPlayerObjectId,
	int? PetObjectId,
	string? PetName,
	int? PetTemplateId,
	float? X,
	float? Y,
	float? Z,
	float? TargetX,
	float? TargetY,
	float? TargetZ,
	byte? Heading,
	int? PetMasterObjectId,
	int? CommonDataDecoration,
	bool MasterIsInFlyingState);

public sealed record PlayerKnownListPetSpawnSnapshotProviderResult(
	PlayerKnownListPetSpawnSnapshotProviderStatus Status,
	SmPetSpawnSnapshot? Snapshot,
	bool MasterIsInFlyingState,
	bool CanCreateFlyStartEmote,
	bool IsLive,
	bool IsJavaPetParity,
	string JavaSource,
	string Notes);

public sealed class PlayerKnownListPetSpawnSnapshotProviderService
{
	public PlayerKnownListPetSpawnSnapshotProviderResult Create(PlayerKnownListPetSpawnSnapshotProviderInput input)
	{
		// Java parity breadcrumb: SM_PET(Pet) reads live Pet, PetCommonData,
		// PetTemplate, CreatureMoveController target, heading, and master object id.
		const string javaSource =
			"com.aionemu.gameserver.network.aion.serverpackets.SM_PET(Pet); "
			+ "com.aionemu.gameserver.model.gameobjects.Pet; "
			+ "com.aionemu.gameserver.model.gameobjects.player.PetCommonData";

		if (input.PetObjectId is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingActivePet, input, javaSource, "No active pet object id was supplied; Java VisibleObjectSpawner.spawnPet would not produce SM_PET(Pet).");
		if (string.IsNullOrEmpty(input.PetName))
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetName, input, javaSource, "Java SM_PET(Pet) writes pet.getName(), sourced from PetCommonData.getName().");
		if (input.PetTemplateId is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetTemplate, input, javaSource, "Java SM_PET(Pet) writes pet.getObjectTemplate().getTemplateId().");
		if (input.X is null || input.Y is null || input.Z is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetPosition, input, javaSource, "Java SM_PET(Pet) writes pet.getPosition().getX/Y/Z().");
		if (input.TargetX is null || input.TargetY is null || input.TargetZ is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetMoveTarget, input, javaSource, "Java SM_PET(Pet) writes pet.getMoveController().getTargetX2/Y2/Z2().");
		if (input.Heading is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetHeading, input, javaSource, "Java SM_PET(Pet) writes pet.getHeading().");
		if (input.PetMasterObjectId is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetMaster, input, javaSource, "Java SM_PET(Pet) writes pet.getMaster().getObjectId().");
		if (input.CommonDataDecoration is null)
			return Blocked(PlayerKnownListPetSpawnSnapshotProviderStatus.MissingPetCommonData, input, javaSource, "Java SM_PET(Pet) writes pet.getCommonData().getDecoration().");

		var snapshot = new SmPetSpawnSnapshot(
			input.PetName,
			input.PetTemplateId.Value,
			input.PetObjectId.Value,
			input.X.Value,
			input.Y.Value,
			input.Z.Value,
			input.TargetX.Value,
			input.TargetY.Value,
			input.TargetZ.Value,
			input.Heading.Value,
			input.PetMasterObjectId.Value,
			input.CommonDataDecoration.Value);

		return new PlayerKnownListPetSpawnSnapshotProviderResult(
			PlayerKnownListPetSpawnSnapshotProviderStatus.Created,
			snapshot,
			input.MasterIsInFlyingState,
			CanCreateFlyStartEmote: input.MasterIsInFlyingState,
			IsLive: false,
			IsJavaPetParity: false,
			javaSource,
			"Created from a supplied packet-facing pet snapshot. C# still lacks live active pet/common-data/template hydration.");
	}

	public bool TryCreate(PlayerKnownListPetSpawnSnapshotProviderInput input, out SmPetSpawnSnapshot snapshot)
	{
		var result = Create(input);
		if (result.Snapshot is { } created)
		{
			snapshot = created;
			return true;
		}

		snapshot = null!;
		return false;
	}

	private static PlayerKnownListPetSpawnSnapshotProviderResult Blocked(
		PlayerKnownListPetSpawnSnapshotProviderStatus status,
		PlayerKnownListPetSpawnSnapshotProviderInput input,
		string javaSource,
		string notes) =>
		new(
			status,
			Snapshot: null,
			input.MasterIsInFlyingState,
			CanCreateFlyStartEmote: false,
			IsLive: false,
			IsJavaPetParity: false,
			javaSource,
			notes);
}
