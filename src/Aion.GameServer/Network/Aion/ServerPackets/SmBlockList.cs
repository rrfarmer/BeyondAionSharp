using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBlockList : GameServerPacket
{
	public const int PacketOpCode = 224;

	private readonly IReadOnlyList<PlayerBlockedUser> _blockedUsers;

	public SmBlockList(IReadOnlyList<PlayerBlockedUser> blockedUsers)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BLOCK_LIST.
		_blockedUsers = blockedUsers;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_BLOCK_LIST.writeImpl.
		buffer.WriteH((-_blockedUsers.Count) & 0xffff);
		buffer.WriteC(0);
		foreach (var blockedUser in _blockedUsers)
		{
			buffer.WriteS(blockedUser.Name);
			buffer.WriteS(blockedUser.Reason);
		}
	}
}
