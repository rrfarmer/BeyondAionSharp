using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionUploadEmblem : GameClientPacket
{
	public CmLegionUploadEmblem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_LEGION_UPLOAD_EMBLEM.readImpl: size + binary emblem data.
	public int Size { get; private set; }
	public byte[] Data { get; private set; } = Array.Empty<byte>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_UPLOAD_EMBLEM.readImpl.
		Size = buffer.ReadD();
		Data = buffer.ReadB(Size);
	}
}
