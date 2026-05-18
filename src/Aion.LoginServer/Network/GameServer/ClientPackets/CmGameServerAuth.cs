using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmGameServerAuth : GsClientPacket
{
	public CmGameServerAuth(byte opCode)
		: base(opCode)
	{
	}

	public byte GameServerId { get; private set; }

	public string Password { get; private set; } = string.Empty;

	public byte[] Ip { get; private set; } = Array.Empty<byte>();

	public ushort Port { get; private set; }

	public byte MinAccessLevel { get; private set; }

	public int MaxPlayers { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		GameServerId = buffer.ReadC();
		Password = buffer.ReadS();
		var length = buffer.ReadC();
		Ip = buffer.ReadB(length);
		Port = buffer.ReadH();
		MinAccessLevel = buffer.ReadC();
		MaxPlayers = buffer.ReadD();
	}
}
