using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public sealed class CmPlayerAuth : GsClientPacket
{
	public CmPlayerAuth(byte opCode)
		: base(opCode)
	{
	}

	public int PlayerId { get; private set; }

	public string AccountName { get; private set; } = string.Empty;

	public string Nickname { get; private set; } = string.Empty;

	public int RaceId { get; private set; }

	public byte AccessLevel { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		PlayerId = buffer.ReadD();
		AccountName = buffer.ReadS();
		Nickname = buffer.ReadS();
		RaceId = buffer.ReadD();
		AccessLevel = buffer.ReadC();
	}
}
