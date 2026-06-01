using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmVersionCheck : GameClientPacket
{
	public CmVersionCheck(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int AionClientVersion { get; private set; }
	public int NpcScriptInterfaceVersion { get; private set; }
	public int WindowsEncoding { get; private set; }
	public int WindowsVersion { get; private set; }
	public int WindowsSubVersion { get; private set; }
	public int LiteInfo { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_VERSION_CHECK.readImpl.
		AionClientVersion = buffer.ReadH();
		NpcScriptInterfaceVersion = buffer.ReadH();
		WindowsEncoding = buffer.ReadD();
		WindowsVersion = buffer.ReadD();
		WindowsSubVersion = buffer.ReadD();
		LiteInfo = buffer.ReadC();
	}
}
