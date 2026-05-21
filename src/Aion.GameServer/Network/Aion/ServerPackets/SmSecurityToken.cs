using System.Text;
using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSecurityToken : GameServerPacket
{
	public const int PacketOpCode = 152;
	private readonly byte[] _token;

	public SmSecurityToken(string token)
		: this(Encoding.ASCII.GetBytes(token))
	{
	}

	public SmSecurityToken(byte[] token)
		: base(PacketOpCode)
	{
		_token = token;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SECURITY_TOKEN.writeImpl.
		buffer.WriteC(0);
		buffer.WriteB(_token);
		buffer.WriteB(new byte[_token.Length]);
	}
}
