using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmKiskUpdate : GameServerPacket
{
	public const int PacketOpCode = 144;

	private readonly PlayerKiskRuntimeState? _kisk;
	private readonly global::Aion.GameServer.Model.GameObjects.Kisk? _faithfulKisk;
	private readonly DateTimeOffset _now;

	public SmKiskUpdate(PlayerKiskRuntimeState kisk, DateTimeOffset? now = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_KISK_UPDATE.
		_kisk = kisk;
		_now = now ?? DateTimeOffset.UtcNow;
	}

	public SmKiskUpdate(global::Aion.GameServer.Model.GameObjects.Kisk kisk)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_KISK_UPDATE(Kisk kisk).
		_faithfulKisk = kisk;
		_now = DateTimeOffset.UtcNow;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		if (_faithfulKisk != null)
		{
			// Java parity: SM_KISK_UPDATE.writeImpl over faithful Kisk.
			buffer.WriteD(_faithfulKisk.GetObjectId());
			buffer.WriteD(_faithfulKisk.GetCreatorId());
			buffer.WriteD(_faithfulKisk.GetUseMask());
			buffer.WriteD(_faithfulKisk.GetCurrentMemberCount());
			buffer.WriteD(_faithfulKisk.GetMaxMembers());
			buffer.WriteD(_faithfulKisk.GetRemainingResurrects());
			buffer.WriteD(_faithfulKisk.GetMaxRessurects());
			buffer.WriteD(_faithfulKisk.GetRemainingLifetime());
			return;
		}

		buffer.WriteD(_kisk!.ObjectId);
		buffer.WriteD(_kisk.OwnerObjectId);
		buffer.WriteD(_kisk.UseMask);
		buffer.WriteD(_kisk.CurrentMemberCount);
		buffer.WriteD(_kisk.MaxMembers);
		buffer.WriteD(_kisk.RemainingResurrects);
		buffer.WriteD(_kisk.MaxResurrects);
		buffer.WriteD(_kisk.GetRemainingLifetimeSeconds(_now));
	}
}
