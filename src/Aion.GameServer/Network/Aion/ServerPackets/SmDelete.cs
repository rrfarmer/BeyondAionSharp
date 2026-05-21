using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDelete : GameServerPacket
{
	public const int PacketOpCode = 22;

	private readonly int _objectId;
	private readonly byte _animationId;

	public SmDelete(int objectId, byte animationId = 1)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE(ObjectDeleteAnimation.FADE_OUT).
		_objectId = objectId;
		_animationId = animationId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_objectId);
		buffer.WriteC(_animationId);
	}
}
