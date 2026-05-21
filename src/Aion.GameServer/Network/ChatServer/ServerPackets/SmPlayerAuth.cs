using Aion.Commons.Network;

namespace Aion.GameServer.Network.ChatServer.ServerPackets;

public sealed class SmPlayerAuth : ChatServerPacket
{
	private readonly int _playerId;
	private readonly string _accountName;
	private readonly string _nickname;
	private readonly int _raceId;
	private readonly byte _accessLevel;

	public SmPlayerAuth(int playerId, string accountName, string nickname, int raceId, byte accessLevel)
	{
		// Java parity: network/chatserver/serverpackets/SM_CS_PLAYER_AUTH(Player).
		_playerId = playerId;
		_accountName = accountName;
		_nickname = nickname;
		_raceId = raceId;
		_accessLevel = accessLevel;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: network/chatserver/serverpackets/SM_CS_PLAYER_AUTH.writeImpl.
		buffer.WriteC(0x01);
		buffer.WriteD(_playerId);
		buffer.WriteS(_accountName);
		buffer.WriteS(_nickname);
		buffer.WriteD(_raceId);
		buffer.WriteC(_accessLevel);
	}
}
