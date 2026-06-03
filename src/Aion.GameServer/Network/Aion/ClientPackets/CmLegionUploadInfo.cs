using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionUploadInfo : GameClientPacket
{
	public CmLegionUploadInfo(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_LEGION_UPLOAD_INFO.readImpl: totalSize + RGBA color channels.
	public int TotalSize { get; private set; }
	public byte Alpha { get; private set; }
	public byte Red { get; private set; }
	public byte Green { get; private set; }
	public byte Blue { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_UPLOAD_INFO.readImpl.
		TotalSize = buffer.ReadD();
		Alpha = buffer.ReadC();
		Red = buffer.ReadC();
		Green = buffer.ReadC();
		Blue = buffer.ReadC();
	}
}
