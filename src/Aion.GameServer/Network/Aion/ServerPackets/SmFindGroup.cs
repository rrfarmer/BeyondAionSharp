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
	private readonly bool _showEnterInstanceMessage;
	private readonly int _lastUpdate;
	private readonly IReadOnlyList<int> _instanceMaskIds;
	private readonly IReadOnlyList<FindGroupRecruitmentSnapshot> _recruitments;
	private readonly FindGroupInstanceGroupWindowSnapshot? _instanceGroupWindow;
	private readonly FindGroupInstanceApplicantSnapshot? _instanceApplicant;
	private readonly IReadOnlyList<FindGroupInstanceGroupRegistrationSnapshot> _instanceGroups;
	private readonly FindGroupInstanceGroupMemberInfoSnapshot? _memberInfo;
	private readonly FindGroupInstanceGroupPrepareWindowSnapshot? _prepareWindow;

	private SmFindGroup(
		int action,
		int idToDelete = 0,
		byte serverId = 0,
		byte unknown1 = 0,
		byte unknown2 = 0,
		byte unknown3 = 0,
		bool showEnterInstanceMessage = false,
		int lastUpdate = 0,
		IReadOnlyList<int>? instanceMaskIds = null,
		IReadOnlyList<FindGroupRecruitmentSnapshot>? recruitments = null,
		FindGroupInstanceGroupWindowSnapshot? instanceGroupWindow = null,
		FindGroupInstanceApplicantSnapshot? instanceApplicant = null,
		IReadOnlyList<FindGroupInstanceGroupRegistrationSnapshot>? instanceGroups = null,
		FindGroupInstanceGroupMemberInfoSnapshot? memberInfo = null,
		FindGroupInstanceGroupPrepareWindowSnapshot? prepareWindow = null)
		: base(PacketOpCode)
	{
		_action = action;
		_idToDelete = idToDelete;
		_serverId = serverId;
		_unknown1 = unknown1;
		_unknown2 = unknown2;
		_unknown3 = unknown3;
		_showEnterInstanceMessage = showEnterInstanceMessage;
		_lastUpdate = lastUpdate;
		_instanceMaskIds = instanceMaskIds ?? [];
		_recruitments = recruitments ?? [];
		_instanceGroupWindow = instanceGroupWindow;
		_instanceApplicant = instanceApplicant;
		_instanceGroups = instanceGroups ?? [];
		_memberInfo = memberInfo;
		_prepareWindow = prepareWindow;
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

	public static SmFindGroup ShowRecruitments(int lastUpdate, IReadOnlyList<FindGroupRecruitmentSnapshot> recruitments)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 0 showRecruitments.
		return new SmFindGroup(0, lastUpdate: lastUpdate, recruitments: recruitments);
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

	public static SmFindGroup ShowInstanceGroups(int lastUpdate, IReadOnlyList<FindGroupInstanceGroupRegistrationSnapshot> instanceGroups)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 10 showInstanceGroups.
		return new SmFindGroup(10, lastUpdate: lastUpdate, instanceGroups: instanceGroups);
	}

	public static SmFindGroup RegisterInstanceGroup(IReadOnlyList<FindGroupInstanceGroupRegistrationSnapshot> instanceGroups)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 14 registerInstanceGroup.
		return new SmFindGroup(14, instanceGroups: instanceGroups);
	}

	public static SmFindGroup ShowInstanceGroupMemberInfo(FindGroupInstanceGroupMemberInfoSnapshot instanceGroup)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 16 showInstanceGroupMemberInfo.
		return new SmFindGroup(16, memberInfo: instanceGroup);
	}

	public static SmFindGroup DestroyPrepareForEntryWindow(
		FindGroupInstanceGroupWindowSnapshot instanceGroup,
		bool showEnterInstanceMessage)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 23 destroyPrepareForEntryWindow.
		return new SmFindGroup(23, showEnterInstanceMessage: showEnterInstanceMessage, instanceGroupWindow: instanceGroup);
	}

	public static SmFindGroup UpdatePrepareForEntryWindow(FindGroupInstanceGroupPrepareWindowSnapshot instanceGroup)
	{
		// Java parity: network/aion/serverpackets/SM_FIND_GROUP action 24 updatePrepareForEntryWindow.
		return new SmFindGroup(24, prepareWindow: instanceGroup);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
		switch (_action)
		{
			case 0:
				buffer.WriteH(_recruitments.Count);
				buffer.WriteH(_recruitments.Count);
				buffer.WriteD(_lastUpdate);
				foreach (var recruitment in _recruitments)
				{
					buffer.WriteD(recruitment.ObjectId);
					buffer.WriteC(recruitment.ServerId);
					buffer.WriteC(0);
					buffer.WriteC(0);
					buffer.WriteC(recruitment.IsSoloPlayer ? 16 : 0);
					buffer.WriteC(recruitment.GroupType);
					buffer.WriteS(recruitment.Message);
					buffer.WriteS(recruitment.RecruiterName);
					buffer.WriteC(recruitment.Size);
					buffer.WriteC(recruitment.MinLevel);
					buffer.WriteC(recruitment.MaxLevel);
					buffer.WriteD(recruitment.LastUpdate);
				}
				break;
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
			case 10:
				buffer.WriteH(_instanceGroups.Count);
				buffer.WriteH(_instanceGroups.Count);
				buffer.WriteD(_lastUpdate);
				foreach (var group in _instanceGroups)
				{
					buffer.WriteD(group.GroupEntryId);
					buffer.WriteD(group.InstanceMaskId);
					buffer.WriteD(1);
					buffer.WriteC(group.MemberCount);
					buffer.WriteC(group.MinMembers);
					buffer.WriteH(0);
					buffer.WriteD(group.RecruiterObjectId);
					buffer.WriteD(1);
					buffer.WriteD(0);
					buffer.WriteC(group.MinLevel);
					buffer.WriteC(group.MaxLevel);
					buffer.WriteH(0);
					buffer.WriteD(group.LastUpdate);
					buffer.WriteD(0);
					buffer.WriteS(group.RecruiterName);
					buffer.WriteS(group.Message);
				}
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
			case 14:
				buffer.WriteC(1);
				foreach (var group in _instanceGroups)
				{
					buffer.WriteD(group.GroupEntryId);
					buffer.WriteD(group.InstanceMaskId);
					buffer.WriteD(1);
					buffer.WriteC(group.MemberCount);
					buffer.WriteC(group.MinMembers);
					buffer.WriteH(0);
					buffer.WriteD(group.RecruiterObjectId);
					buffer.WriteC(1);
					buffer.WriteC(0);
					buffer.WriteD(1);
					buffer.WriteH(0);
					buffer.WriteC(group.MinLevel);
					buffer.WriteC(group.MaxLevel);
					buffer.WriteH(0);
					buffer.WriteD(group.LastUpdate);
					buffer.WriteD(0);
					buffer.WriteS(group.RecruiterName);
					buffer.WriteS(group.Message);
				}
				break;
			case 16:
				var memberInfo = _memberInfo ?? throw new InvalidOperationException("Instance group member-info snapshot is required.");
				buffer.WriteH(memberInfo.Members.Count);
				buffer.WriteH(memberInfo.Members.Count);
				buffer.WriteD(memberInfo.LastUpdate);
				foreach (var member in memberInfo.Members)
				{
					buffer.WriteD(0);
					buffer.WriteD(member.WorldId);
					buffer.WriteD(member.PlayerObjectId);
					buffer.WriteD(member.Level);
					buffer.WriteD(member.ClassId);
					buffer.WriteH(1);
					buffer.WriteC(0);
					buffer.WriteC(0);
					buffer.WriteS(member.Name);
				}
				break;
			case 23:
				var prepareWindowGroup = _instanceGroupWindow ?? throw new InvalidOperationException("Instance group window snapshot is required.");
				buffer.WriteD(prepareWindowGroup.GroupEntryId);
				buffer.WriteD(prepareWindowGroup.InstanceMaskId);
				buffer.WriteC(_showEnterInstanceMessage ? 1 : 0);
				break;
			case 24:
				var prepareWindow = _prepareWindow ?? throw new InvalidOperationException("Prepare window snapshot is required.");
				buffer.WriteD(prepareWindow.GroupEntryId);
				buffer.WriteD(prepareWindow.InstanceMaskId);
				buffer.WriteC(prepareWindow.Members.Count);
				foreach (var member in prepareWindow.Members)
				{
					buffer.WriteD(0);
					buffer.WriteD(0);
					buffer.WriteD(member.PlayerObjectId);
					buffer.WriteD(member.Level);
					buffer.WriteD(member.ClassId);
					buffer.WriteH(0);
					buffer.WriteC(1);
					buffer.WriteC(member.IsOnline ? 1 : 0);
					buffer.WriteS(member.Name);
				}
				break;
		}
	}
}

public sealed record FindGroupInstanceGroupWindowSnapshot(int GroupEntryId, int InstanceMaskId);

public sealed record FindGroupRecruitmentSnapshot(
	int ObjectId,
	byte ServerId,
	bool IsSoloPlayer,
	byte GroupType,
	string Message,
	string RecruiterName,
	byte Size,
	byte MinLevel,
	byte MaxLevel,
	int LastUpdate);

public sealed record FindGroupInstanceApplicantSnapshot(int PlayerObjectId, byte ClassId, int Level, string Name);

public sealed record FindGroupInstanceGroupRegistrationSnapshot(
	int GroupEntryId,
	int InstanceMaskId,
	int MemberCount,
	int MinMembers,
	int RecruiterObjectId,
	int MinLevel,
	int MaxLevel,
	int LastUpdate,
	string RecruiterName,
	string Message);

public sealed record FindGroupInstanceGroupMemberInfoSnapshot(
	int LastUpdate,
	IReadOnlyList<FindGroupInstanceGroupMemberInfoMemberSnapshot> Members);

public sealed record FindGroupInstanceGroupMemberInfoMemberSnapshot(
	int WorldId,
	int PlayerObjectId,
	int Level,
	int ClassId,
	string Name);

public sealed record FindGroupInstanceGroupPrepareWindowSnapshot(
	int GroupEntryId,
	int InstanceMaskId,
	IReadOnlyList<FindGroupInstanceGroupPrepareMemberSnapshot> Members);

public sealed record FindGroupInstanceGroupPrepareMemberSnapshot(
	int PlayerObjectId,
	int Level,
	int ClassId,
	bool IsOnline,
	string Name);
