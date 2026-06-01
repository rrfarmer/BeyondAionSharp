using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGroupDataExchange : GameClientPacket
{
	public CmGroupDataExchange(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Action { get; private set; }
	public byte GroupType { get; private set; }
	public byte Unknown2 { get; private set; }
	public byte[] Data { get; private set; } = [];

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GROUP_DATA_EXCHANGE.readImpl.
		Action = buffer.ReadC();
		if (Action != 1)
		{
			GroupType = buffer.ReadC();
			Unknown2 = buffer.ReadC();
		}

		var dataSize = buffer.ReadD();
		Data = buffer.ReadB(dataSize);
	}
}
