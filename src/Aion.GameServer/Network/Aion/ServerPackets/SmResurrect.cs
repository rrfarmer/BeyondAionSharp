using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmResurrect : GameServerPacket
{
	public const int PacketOpCode = 194;

	private readonly string _creatureName;
	private readonly int _skillId;

	public SmResurrect(string creatureName, int skillId = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RESURRECT(Creature, int) stores creature.getName() and resurrection skill id.
		_creatureName = creatureName ?? string.Empty;
		_skillId = skillId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteS(_creatureName);
		buffer.WriteH(_skillId);
		buffer.WriteD(0);
	}
}
