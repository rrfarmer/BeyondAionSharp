using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCraftAnimation : GameServerPacket
{
	public const int PacketOpCode = 180;

	private readonly int _playerObjectId;
	private readonly int _targetObjectId;
	private readonly int _skillId;
	private readonly int _action;

	public SmCraftAnimation(int playerObjectId, int targetObjectId, int skillId, int action)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_CRAFT_ANIMATION.
		_playerObjectId = playerObjectId;
		_targetObjectId = targetObjectId;
		_skillId = skillId;
		_action = action;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_targetObjectId);
		buffer.WriteH(_skillId);
		buffer.WriteC(_action);
	}
}
