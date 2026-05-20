using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAccountProperties : GameServerPacket
{
	public const int PacketOpCode = 238;

	private readonly bool _gmPanelEnabled;

	public SmAccountProperties(bool gmPanelEnabled = false)
		: base(PacketOpCode)
	{
		_gmPanelEnabled = gmPanelEnabled;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ACCOUNT_PROPERTIES.writeImpl.
		buffer.WriteC(_gmPanelEnabled ? 1 : 0);
		buffer.WriteD(0);
		buffer.WriteH(0);
		buffer.WriteC(0);
		buffer.WriteH(0);
		buffer.WriteC(0);
		buffer.WriteH(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(4);
	}
}
