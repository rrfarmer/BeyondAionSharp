using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFindGroup : GameServerPacket
{
	public const int PacketOpCode = 166;

	private readonly int _action;
	private readonly int _idToDelete;
	private readonly byte _serverId;
	private readonly byte _unknown1;
	private readonly byte _unknown2;
	private readonly byte _unknown3;
	private readonly IReadOnlyList<int> _instanceMaskIds;
	private readonly FindGroupInstanceGroupWindowSnapshot? _instanceGroupWindow;
	private readonly FindGroupInstanceApplicantSnapshot? _instanceApplicant;

	private SmFindGroup(
		int action,
		int idToDelete = 0,
		byte serverId = 0,
		byte unknown1 = 0,
		byte unknown2 = 0,
		byte unknown3 = 0,
		IReadOnlyList<int>? instanceMaskIds = null,
		FindGroupInstanceGroupWindowSnapshot? instanceGroupWindow = null,
		FindGroupInstanceApplicantSnapshot? instanceApplicant = null)
		: base(PacketOpCode)
	{
		_action = action;
		_idToDelete = idToDelete;
		_serverId = serverId;
		_unknown1 = unknown1;
		_unknown2 = unknown2;
		_unknown3 = unknown3;
		_instanceMaskIds = instanceMaskIds ?? [];
		_instanceGroupWindow = instanceGroupWindow;
		_instanceApplicant = instanceApplicant;
	}

	public static SmFindGroup RemoveRecruitment(int recruitmentIdToDelete, byte serverId, byte unknown1, byte unknown2, byte unknown3)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP(int, byte, byte, byte, byte).
		return new SmFindGroup(1, recruitmentIdToDelete, serverId, unknown1, unknown2, unknown3);
	}

	public static SmFindGroup RemoveApplication(int applicationIdToDelete)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP(int applicationIdToDelete).
		return new SmFindGroup(5, applicationIdToDelete);
	}

	public static SmFindGroup EnableRegisterForInstances(IReadOnlyList<int> instanceMaskIds)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP(List<Integer> instanceMaskIds).
		return new SmFindGroup(26, instanceMaskIds: instanceMaskIds);
	}

	public static SmFindGroup ShowEnterButtonInPrepareForEntryWindow(FindGroupInstanceGroupWindowSnapshot instanceGroup)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 18 showEnterButtonInPrepareForEntryWindow.
		return new SmFindGroup(18, instanceGroupWindow: instanceGroup);
	}

	public static SmFindGroup ShowPrepareForEntryWindow(FindGroupInstanceGroupWindowSnapshot instanceGroup)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 22 showPrepareForEntryWindow.
		return new SmFindGroup(22, instanceGroupWindow: instanceGroup);
	}

	public static SmFindGroup SendInstanceGroupApplicationAsWhisperChatMessage(FindGroupInstanceApplicantSnapshot instanceApplicant)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP(Player instanceApplicant), action 11.
		return new SmFindGroup(11, instanceApplicant: instanceApplicant);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
		switch (_action)
		{
			case 1:
				buffer.WriteD(_idToDelete);
				buffer.WriteC(_serverId);
				buffer.WriteC(_unknown1);
				buffer.WriteC(_unknown2);
				buffer.WriteC(_unknown3);
				break;
			case 5:
				buffer.WriteD(_idToDelete);
				break;
			case 26:
				buffer.WriteH(_instanceMaskIds.Count);
				foreach (var instanceMaskId in _instanceMaskIds)
					buffer.WriteD(instanceMaskId);
				break;
			case 18:
			case 22:
				var instanceGroup = _instanceGroupWindow ?? throw new InvalidOperationException("Instance group window snapshot is required.");
				buffer.WriteD(instanceGroup.GroupEntryId);
				buffer.WriteD(instanceGroup.InstanceMaskId);
				break;
			case 11:
				var applicant = _instanceApplicant ?? throw new InvalidOperationException("Instance applicant snapshot is required.");
				buffer.WriteD(applicant.PlayerObjectId);
				buffer.WriteD(0);
				buffer.WriteD(0);
				buffer.WriteH(0);
				buffer.WriteC(0);
				buffer.WriteC(applicant.ClassId);
				buffer.WriteD(applicant.Level);
				buffer.WriteS(applicant.Name);
				break;
		}
	}
}

public sealed record FindGroupInstanceGroupWindowSnapshot(int GroupEntryId, int InstanceMaskId);

public sealed record FindGroupInstanceApplicantSnapshot(int PlayerObjectId, byte ClassId, int Level, string Name);
