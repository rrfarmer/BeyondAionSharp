using Aion.Commons.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed record SmPetSpawnSnapshot(
	string Name,
	int TemplateId,
	int ObjectId,
	float X,
	float Y,
	float Z,
	float TargetX,
	float TargetY,
	float TargetZ,
	byte Heading,
	int MasterObjectId,
	int Decoration);

public sealed class SmPet : GameServerPacket
{
	public const int PacketOpCode = 101;

	private readonly PetAction _action;
	private readonly SmPetSpawnSnapshot? _spawn;
	private readonly int _petObjectId;
	private readonly ObjectDeleteAnimation _animation;

	public SmPet(PetAction action)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET(PetAction) action-only branches.
		if (!IsActionOnly(action))
		{
			throw new ArgumentOutOfRangeException(nameof(action), action, "SM_PET action is not a supported action-only packet.");
		}

		_action = action;
	}

	public SmPet(SmPetSpawnSnapshot spawn)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET(Pet) with PetAction.SPAWN.
		_action = PetAction.Spawn;
		_spawn = spawn;
	}

	public SmPet(int petObjectId, ObjectDeleteAnimation animation)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET(int, ObjectDeleteAnimation) with PetAction.DISMISS.
		_action = PetAction.Dismiss;
		_petObjectId = petObjectId;
		_animation = animation;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_PET.writeImpl known-list spawn/dismiss subset.
		buffer.WriteH((int)_action);

		switch (_action)
		{
			case PetAction.Spawn:
				WriteSpawn(buffer, _spawn ?? throw new InvalidOperationException("Spawn snapshot is required for SM_PET spawn."));
				break;
			case PetAction.Dismiss:
				buffer.WriteD(_petObjectId);
				buffer.WriteC((byte)_animation);
				break;
			case PetAction.TalkWithMerchant:
			case PetAction.TalkWithMinder:
			case PetAction.HAdopt:
			case PetAction.HAbandon:
				break;
			default:
				throw new NotSupportedException($"SM_PET action {_action} is not ported in the known-list serializer subset.");
		}
	}

	private static void WriteSpawn(PacketBuffer buffer, SmPetSpawnSnapshot spawn)
	{
		buffer.WriteS(spawn.Name);
		buffer.WriteD(spawn.TemplateId);
		buffer.WriteD(spawn.ObjectId);
		buffer.WriteF(spawn.X);
		buffer.WriteF(spawn.Y);
		buffer.WriteF(spawn.Z);
		buffer.WriteF(spawn.TargetX);
		buffer.WriteF(spawn.TargetY);
		buffer.WriteF(spawn.TargetZ);
		buffer.WriteC(spawn.Heading);
		buffer.WriteD(spawn.MasterObjectId);
		WriteAppearance(buffer, spawn.Decoration);
	}

	private static void WriteAppearance(PacketBuffer buffer, int decoration)
	{
		buffer.WriteH((int)PetFunctionType.Appearance);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteD(decoration);
		buffer.WriteD(0);
		buffer.WriteD(0);
	}

	private static bool IsActionOnly(PetAction action) => action switch
	{
		PetAction.TalkWithMerchant or
			PetAction.TalkWithMinder or
			PetAction.HAdopt or
			PetAction.HAbandon => true,
		_ => false,
	};
}
