using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmUseObject : GameServerPacket
{
	public const int PacketOpCode = 197;

	private readonly int _playerObjectId;
	private readonly int _targetObjectId;
	private readonly int _time;
	private readonly int _actionType;

	public SmUseObject(int playerObjectId, int targetObjectId, int time, int actionType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_USE_OBJECT.
		_playerObjectId = playerObjectId;
		_targetObjectId = targetObjectId;
		_time = time;
		_actionType = actionType;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_targetObjectId);
		buffer.WriteD(_time);
		buffer.WriteC(_actionType);
	}
}
