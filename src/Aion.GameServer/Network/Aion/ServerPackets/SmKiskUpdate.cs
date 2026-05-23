using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmKiskUpdate : GameServerPacket
{
	public const int PacketOpCode = 144;

	private readonly PlayerKiskRuntimeState _kisk;
	private readonly DateTimeOffset _now;

	public SmKiskUpdate(PlayerKiskRuntimeState kisk, DateTimeOffset? now = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_KISK_UPDATE.
		_kisk = kisk;
		_now = now ?? DateTimeOffset.UtcNow;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_kisk.ObjectId);
		buffer.WriteD(_kisk.OwnerObjectId);
		buffer.WriteD(_kisk.UseMask);
		buffer.WriteD(_kisk.CurrentMemberCount);
		buffer.WriteD(_kisk.MaxMembers);
		buffer.WriteD(_kisk.RemainingResurrects);
		buffer.WriteD(_kisk.MaxResurrects);
		buffer.WriteD(_kisk.GetRemainingLifetimeSeconds(_now));
	}
}
