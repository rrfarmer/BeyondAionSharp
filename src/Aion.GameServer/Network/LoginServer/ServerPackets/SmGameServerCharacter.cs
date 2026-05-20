using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

public sealed class SmGameServerCharacter : LoginServerPacket
{
	public SmGameServerCharacter(int accountId, int characterCount)
	{
		AccountId = accountId;
		CharacterCount = characterCount;
	}

	public int AccountId { get; }

	public int CharacterCount { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_GS_CHARACTER.writeImpl.
		buffer.WriteC(0x08);
		buffer.WriteD(AccountId);
		buffer.WriteC(CharacterCount);
	}
}
