using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionEdit : GameServerPacket
{
	public const int PacketOpCode = 158;
	private const int LevelEditType = 0x00;
	private const int RankingPositionEditType = 0x01;
	private const int PermissionsEditType = 0x02;
	private const int ContributionEditType = 0x03;
	private const int WarehouseKinahEditType = 0x04;
	private const int AnnouncementEditType = 0x05;
	private const int DisbandEditType = 0x06;
	private const int RecoverEditType = 0x07;
	private const int RefreshAnnouncementEditType = 0x08;

	private readonly int _type;
	private readonly int _intValue;
	private readonly long _contributionPoints;
	private readonly long _warehouseKinah;
	private readonly string _announcement;
	private readonly int[] _permissions;

	private SmLegionEdit(
		int type,
		int intValue = 0,
		long contributionPoints = 0,
		long warehouseKinah = 0,
		string? announcement = null,
		int[]? permissions = null)
		: base(PacketOpCode)
	{
		_type = type;
		_intValue = intValue;
		_contributionPoints = contributionPoints;
		_warehouseKinah = warehouseKinah;
		_announcement = announcement ?? string.Empty;
		_permissions = permissions ?? Array.Empty<int>();
	}

	public static SmLegionEdit Level(int legionLevel)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x00 writes legion level.
		return new SmLegionEdit(LevelEditType, intValue: legionLevel);
	}

	public static SmLegionEdit RankingPosition(int rankingListPosition)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x01 writes AbyssRankingCache position.
		return new SmLegionEdit(RankingPositionEditType, intValue: rankingListPosition);
	}

	public static SmLegionEdit Permissions(int deputy, int centurion, int legionary, int volunteer)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x02 writes four permission masks.
		return new SmLegionEdit(PermissionsEditType, permissions: [deputy, centurion, legionary, volunteer]);
	}

	public static SmLegionEdit Contribution(long contributionPoints)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x03 writes legion contribution points.
		return new SmLegionEdit(ContributionEditType, contributionPoints: contributionPoints);
	}

	public static SmLegionEdit WarehouseKinah(long kinah)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x04 writes legion warehouse Kinah.
		return new SmLegionEdit(WarehouseKinahEditType, warehouseKinah: kinah);
	}

	public static SmLegionEdit Announcement(string announcement, int unixTime)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x05 writes message and Unix seconds.
		return new SmLegionEdit(AnnouncementEditType, intValue: unixTime, announcement: announcement);
	}

	public static SmLegionEdit Disband(int unixTime)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x06 writes scheduled Unix seconds.
		return new SmLegionEdit(DisbandEditType, intValue: unixTime);
	}

	public static SmLegionEdit Recover()
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x07 writes only the edit type.
		return new SmLegionEdit(RecoverEditType);
	}

	public static SmLegionEdit RefreshAnnouncement()
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x08 writes only the edit type.
		return new SmLegionEdit(RefreshAnnouncementEditType);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_type);
		switch (_type)
		{
			case LevelEditType:
				buffer.WriteC(_intValue);
				break;
			case RankingPositionEditType:
				buffer.WriteD(_intValue);
				break;
			case PermissionsEditType:
				for (var index = 0; index < 4; index++)
					buffer.WriteH(_permissions[index]);
				break;
			case ContributionEditType:
				buffer.WriteQ(_contributionPoints);
				break;
			case WarehouseKinahEditType:
				buffer.WriteQ(_warehouseKinah);
				break;
			case AnnouncementEditType:
				buffer.WriteS(_announcement);
				buffer.WriteD(_intValue);
				break;
			case DisbandEditType:
				buffer.WriteD(_intValue);
				break;
		}
	}
}
