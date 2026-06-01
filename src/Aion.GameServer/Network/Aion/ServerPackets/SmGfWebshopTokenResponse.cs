using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGfWebshopTokenResponse : GameServerPacket
{
	public const int PacketOpCode = 274;
	private const int TokenFixedLength = 32;
	private readonly string _token;

	public SmGfWebshopTokenResponse(string token)
		: base(PacketOpCode)
	{
		_token = token;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_GF_WEBSHOP_TOKEN_RESPONSE.writeImpl writes writeS(token, 32).
		WriteFixedUtf16String(buffer, _token, TokenFixedLength);
	}

	private static void WriteFixedUtf16String(PacketBuffer buffer, string? value, int fixedLength)
	{
		if (string.IsNullOrEmpty(value))
		{
			buffer.WriteB(new byte[(fixedLength * 2) + 2]);
			return;
		}

		for (var i = 0; i < fixedLength; i++)
			buffer.WriteH(i < value.Length ? value[i] : 0);

		buffer.WriteH(0);
	}
}
