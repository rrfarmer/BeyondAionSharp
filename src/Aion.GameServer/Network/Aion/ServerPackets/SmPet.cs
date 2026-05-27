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

public sealed record SmPetSurrenderSnapshot(int TemplateId, int ObjectId);

public sealed record SmPetDataSnapshot(
	string Name,
	int TemplateId,
	int ObjectId,
	int MasterObjectId,
	int BirthdayEpochSeconds,
	int SecondsUntilExpiration,
	IReadOnlyList<SmPetFunctionSnapshot> Functions,
	int Decoration);

public sealed record SmPetFunctionSnapshot(
	PetFunctionType FunctionType,
	IReadOnlyList<int>? DopingItemIds = null,
	int FeedProgressData = 0,
	int RefeedDelaySeconds = 0);

public sealed class SmPet : GameServerPacket
{
	private const int PetDopingBagMaxItems = 8;

	public const int PacketOpCode = 101;

	private readonly PetAction _action;
	private readonly SmPetSpawnSnapshot? _spawn;
	private readonly SmPetSurrenderSnapshot? _surrender;
	private readonly SmPetDataSnapshot? _petData;
	private readonly IReadOnlyList<SmPetDataSnapshot>? _pets;
	private readonly int _petObjectId;
	private readonly string? _petName;
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

	public static SmPet Adopt(SmPetDataSnapshot petData)
	{
		// Java parity: network/aion/serverpackets/SM_PET(PetCommonData, true) with PetAction.ADOPT.
		return new SmPet(PetAction.Adopt, petData);
	}

	public static SmPet LoadPets(IReadOnlyList<SmPetDataSnapshot> pets)
	{
		// Java parity: network/aion/serverpackets/SM_PET(Collection<PetCommonData>) with PetAction.LOAD_PETS.
		return new SmPet(pets);
	}

	public SmPet(SmPetSurrenderSnapshot surrender)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET(PetCommonData, false) with PetAction.SURRENDER.
		_action = PetAction.Surrender;
		_surrender = surrender;
	}

	public SmPet(int petObjectId, ObjectDeleteAnimation animation)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET(int, ObjectDeleteAnimation) with PetAction.DISMISS.
		_action = PetAction.Dismiss;
		_petObjectId = petObjectId;
		_animation = animation;
	}

	public SmPet(int petObjectId, string petName)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET(int, String) with PetAction.RENAME.
		_action = PetAction.Rename;
		_petObjectId = petObjectId;
		_petName = petName;
	}

	private SmPet(PetAction action, SmPetDataSnapshot petData)
		: base(PacketOpCode)
	{
		_action = action;
		_petData = petData;
	}

	private SmPet(IReadOnlyList<SmPetDataSnapshot> pets)
		: base(PacketOpCode)
	{
		_action = PetAction.LoadPets;
		_pets = pets;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_PET.writeImpl currently ported packet subset.
		buffer.WriteH((int)_action);

		switch (_action)
		{
			case PetAction.LoadPets:
				WriteLoadPets(buffer, _pets ?? throw new InvalidOperationException("Pet list snapshot is required for SM_PET load pets."));
				break;
			case PetAction.Adopt:
				WritePetData(buffer, _petData ?? throw new InvalidOperationException("Pet data snapshot is required for SM_PET adopt."));
				break;
			case PetAction.Spawn:
				WriteSpawn(buffer, _spawn ?? throw new InvalidOperationException("Spawn snapshot is required for SM_PET spawn."));
				break;
			case PetAction.Surrender:
				WriteSurrender(buffer, _surrender ?? throw new InvalidOperationException("Surrender snapshot is required for SM_PET surrender."));
				break;
			case PetAction.Dismiss:
				buffer.WriteD(_petObjectId);
				buffer.WriteC((byte)_animation);
				break;
			case PetAction.Rename:
				buffer.WriteD(_petObjectId);
				buffer.WriteS(_petName ?? throw new InvalidOperationException("Pet name is required for SM_PET rename."));
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

	internal static void WritePetData(PacketBuffer buffer, SmPetDataSnapshot petData)
	{
		// Java parity: network/aion/serverpackets/SM_PET.writePetData(PetCommonData).
		buffer.WriteS(petData.Name);
		buffer.WriteD(petData.TemplateId);
		buffer.WriteD(petData.ObjectId);
		buffer.WriteD(petData.MasterObjectId);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(petData.BirthdayEpochSeconds);
		buffer.WriteD(petData.SecondsUntilExpiration);

		var writableFunctions = OrderedWritableFunctions(petData.Functions).ToArray();
		if (writableFunctions.Length > 2)
		{
			throw new InvalidOperationException("Java pet static data currently has at most two packet-writable functions per pet.");
		}

		foreach (var function in writableFunctions)
		{
			WritePetFunction(buffer, function);
		}

		if (writableFunctions.Length == 0)
		{
			buffer.WriteH((int)PetFunctionType.None);
			buffer.WriteH((int)PetFunctionType.None);
		}
		else if (writableFunctions.Length == 1)
		{
			buffer.WriteH((int)PetFunctionType.None);
		}

		WriteAppearance(buffer, petData.Decoration);
	}

	private static void WriteLoadPets(PacketBuffer buffer, IReadOnlyList<SmPetDataSnapshot> pets)
	{
		buffer.WriteC(0);
		buffer.WriteH(pets.Count);
		foreach (var pet in pets)
		{
			WritePetData(buffer, pet);
		}
	}

	private static void WriteSurrender(PacketBuffer buffer, SmPetSurrenderSnapshot surrender)
	{
		buffer.WriteD(surrender.TemplateId);
		buffer.WriteD(surrender.ObjectId);
		buffer.WriteD(0);
		buffer.WriteD(0);
	}

	private static IEnumerable<SmPetFunctionSnapshot> OrderedWritableFunctions(IReadOnlyList<SmPetFunctionSnapshot> functions)
	{
		// Java checks functions in this fixed order, not XML order.
		foreach (var type in new[] { PetFunctionType.Warehouse, PetFunctionType.Loot, PetFunctionType.Doping, PetFunctionType.Food })
		{
			foreach (var function in functions)
			{
				if (function.FunctionType == type)
				{
					yield return function;
				}
			}
		}
	}

	private static void WritePetFunction(PacketBuffer buffer, SmPetFunctionSnapshot function)
	{
		switch (function.FunctionType)
		{
			case PetFunctionType.Warehouse:
				buffer.WriteC((byte)PetFunctionType.Warehouse);
				buffer.WriteC(0);
				break;
			case PetFunctionType.Loot:
				buffer.WriteC((byte)PetFunctionType.Loot);
				buffer.WriteC(1);
				buffer.WriteC(0);
				break;
			case PetFunctionType.Doping:
				buffer.WriteC((byte)PetFunctionType.Doping);
				buffer.WriteC(PetDopingBagMaxItems * 4);
				WriteDopingItems(buffer, function.DopingItemIds ?? []);
				break;
			case PetFunctionType.Food:
				buffer.WriteC((byte)PetFunctionType.Food);
				buffer.WriteC(8);
				buffer.WriteD(function.FeedProgressData);
				buffer.WriteD(function.RefeedDelaySeconds);
				break;
			default:
				throw new NotSupportedException($"Pet function {function.FunctionType} is not written by Java SM_PET.writePetData.");
		}
	}

	private static void WriteDopingItems(PacketBuffer buffer, IReadOnlyList<int> itemIds)
	{
		if (itemIds.Count > PetDopingBagMaxItems)
		{
			throw new ArgumentOutOfRangeException(nameof(itemIds), itemIds.Count, "Java PetDopingBag has exactly 8 packet slots.");
		}

		for (var i = 0; i < PetDopingBagMaxItems; i++)
		{
			buffer.WriteD(i < itemIds.Count ? itemIds[i] : 0);
		}
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
