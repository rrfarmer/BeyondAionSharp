using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionEdit : GameServerPacket
{
	public const int PacketOpCode = 158;
	private const int ContributionEditType = 0x03;

	private readonly int _type;
	private readonly long _contributionPoints;

	private SmLegionEdit(int type, long contributionPoints)
		: base(PacketOpCode)
	{
		_type = type;
		_contributionPoints = contributionPoints;
	}

	public static SmLegionEdit Contribution(long contributionPoints)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_EDIT type 0x03 writes legion contribution points.
		return new SmLegionEdit(ContributionEditType, contributionPoints);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_type);
		if (_type == ContributionEditType)
			buffer.WriteQ(_contributionPoints);
	}
}
