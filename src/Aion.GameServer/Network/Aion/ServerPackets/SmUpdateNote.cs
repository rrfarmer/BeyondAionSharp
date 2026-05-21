using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmUpdateNote : GameServerPacket
{
	public const int PacketOpCode = 104;

	private readonly int _targetObjectId;
	private readonly string _note;

	public SmUpdateNote(Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_UPDATE_NOTE(Player).
		_targetObjectId = player.ObjectId;
		_note = player.Note;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_UPDATE_NOTE.writeImpl.
		buffer.WriteD(_targetObjectId);
		buffer.WriteS(_note);
	}
}
