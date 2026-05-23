using Aion.Commons.Network;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDelete : GameServerPacket
{
	public const int PacketOpCode = 22;

	private readonly int _objectId;
	private readonly ObjectDeleteAnimation _animation;

	public SmDelete(int objectId, ObjectDeleteAnimation animation = ObjectDeleteAnimation.FadeOut)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE(ObjectDeleteAnimation.FADE_OUT).
		_objectId = objectId;
		_animation = animation;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_objectId);
		buffer.WriteC((byte)_animation);
	}
}
