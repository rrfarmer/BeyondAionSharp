using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCaptcha : GameClientPacket
{
	public CmCaptcha(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_CAPTCHA.readImpl: type (byte); type 2 adds count+word, type 4 is status check.
	public byte Type { get; private set; }
	public byte Count { get; private set; }
	public string Word { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CAPTCHA.readImpl.
		Type = buffer.ReadC();
		if (Type == 2)
		{
			Count = buffer.ReadC();
			Word = buffer.ReadS();
		}
	}
}
