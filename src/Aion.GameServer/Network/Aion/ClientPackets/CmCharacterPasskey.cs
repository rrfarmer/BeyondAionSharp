using System.Text;
using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCharacterPasskey : GameClientPacket
{
	private const int PasskeyByteLength = 48;

	public CmCharacterPasskey(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Type { get; private set; }

	public string Passkey { get; private set; } = string.Empty;

	public string NewPasskey { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHARACTER_PASSKEY.readImpl.
		Type = buffer.ReadH();
		Passkey = ReadFixedUtf16String(buffer);
		if (Type == 2)
			NewPasskey = ReadFixedUtf16String(buffer);
	}

	private static string ReadFixedUtf16String(PacketBuffer buffer)
	{
		var bytes = buffer.ReadB(PasskeyByteLength);
		return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
	}
}
