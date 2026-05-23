using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDie : GameServerPacket
{
	public const int PacketOpCode = 193;

	private readonly bool _allowReviveBySkill;
	private readonly bool _allowReviveByItem;
	private readonly int _remainingKiskTimeSeconds;
	private readonly bool _allowInstanceRevive;
	private readonly bool _invasion;

	public SmDie(
		bool allowReviveBySkill = false,
		bool allowReviveByItem = false,
		int remainingKiskTimeSeconds = 0,
		bool allowInstanceRevive = false,
		bool invasion = false)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DIE writes resurrection choices and remaining kisk lifetime.
		_allowReviveBySkill = allowReviveBySkill;
		_allowReviveByItem = allowReviveByItem;
		_remainingKiskTimeSeconds = Math.Max(0, remainingKiskTimeSeconds);
		_allowInstanceRevive = allowInstanceRevive;
		_invasion = invasion;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_allowReviveBySkill ? 1 : 0);
		buffer.WriteC(_allowReviveByItem ? 1 : 0);
		buffer.WriteD(_remainingKiskTimeSeconds);
		buffer.WriteC(_allowInstanceRevive ? 1 : 0);
		buffer.WriteC(_invasion ? 0x80 : 0x00);
	}
}
