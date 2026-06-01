using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGameguard : GameClientPacket
{
	public CmGameguard(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Size { get; private set; }

	public byte[] Data { get; private set; } = [];

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GAMEGUARD.readImpl.
		Size = buffer.ReadD();
		Data = buffer.ReadB(Size);
	}
}
