using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmActionAnimation : GameServerPacket
{
	public const int PacketOpCode = 70;
	public const int LevelUp = 0;
	public const int BindKisk = 2;

	private readonly int _targetObjectId;
	private readonly int _actionAnimationId;
	private readonly int _levelOrObjectId;

	public SmActionAnimation(int targetObjectId, int actionAnimationId, int levelOrObjectId = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_ACTION_ANIMATION.
		_targetObjectId = targetObjectId;
		_actionAnimationId = actionAnimationId;
		_levelOrObjectId = levelOrObjectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_targetObjectId);
		buffer.WriteH(_actionAnimationId);
		buffer.WriteD(_levelOrObjectId);
	}
}
