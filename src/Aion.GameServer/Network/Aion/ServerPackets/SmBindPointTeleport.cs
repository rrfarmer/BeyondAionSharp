using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBindPointTeleport : GameServerPacket
{
	public const int PacketOpCode = 296;

	private readonly byte _action;
	private readonly int _playerObjectId;
	private readonly int _locId;
	private readonly int _cooldownSeconds;

	public SmBindPointTeleport(byte action, int playerObjectId, int locId, int cooldownSeconds = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_TELEPORT.writeImpl.
		// Only actions 1 and 3 write locId; action 3 also writes cooldown.
		_action = action;
		_playerObjectId = playerObjectId;
		_locId = locId;
		_cooldownSeconds = cooldownSeconds;
	}

	public static SmBindPointTeleport Start(int playerObjectId, int locId)
	{
		return new SmBindPointTeleport(1, playerObjectId, locId);
	}

	public static SmBindPointTeleport Cancel(int playerObjectId, int locId)
	{
		return new SmBindPointTeleport(2, playerObjectId, locId);
	}

	public static SmBindPointTeleport Cooldown(int playerObjectId, int locId, int cooldownSeconds)
	{
		return new SmBindPointTeleport(3, playerObjectId, locId, cooldownSeconds);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
		buffer.WriteD(_playerObjectId);
		switch (_action)
		{
			case 1:
				buffer.WriteD(_locId);
				break;
			case 3:
				buffer.WriteD(_locId);
				buffer.WriteD(_cooldownSeconds);
				break;
		}
	}
}
