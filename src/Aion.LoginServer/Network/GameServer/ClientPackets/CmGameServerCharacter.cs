using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmGameServerCharacter : GsClientPacket
{
	public CmGameServerCharacter(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public int CharacterCount { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		CharacterCount = buffer.ReadC();
	}
}
