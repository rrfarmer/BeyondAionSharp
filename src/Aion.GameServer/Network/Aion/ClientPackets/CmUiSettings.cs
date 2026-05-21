using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmUiSettings : GameClientPacket
{
	public CmUiSettings(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte SettingsType { get; private set; }

	public int DeclaredSize { get; private set; }

	public byte[] Data { get; private set; } = [];

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_UI_SETTINGS.readImpl.
		SettingsType = buffer.ReadC();
		buffer.ReadH();
		DeclaredSize = buffer.ReadH();
		Data = buffer.ReadRemaining();
	}
}
