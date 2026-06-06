using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionUpdateSelfIntro : GameServerPacket
{
	public const int PacketOpCode = 119; // Java parity: ServerPacketsOpcodes addPacketOpcode(119, SM_LEGION_UPDATE_SELF_INTRO.class).

	private readonly int _playerObjectId;
	private readonly string _selfIntro;

	public SmLegionUpdateSelfIntro(int playerObjectId, string selfIntro)
		: base(PacketOpCode)
	{
		_playerObjectId = playerObjectId;
		_selfIntro = selfIntro;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_SELF_INTRO.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteS(_selfIntro);
	}
}
