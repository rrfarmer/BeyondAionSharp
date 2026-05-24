using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSummonUseSkill : GameServerPacket
{
	public const int PacketOpCode = 162;

	private readonly int _summonObjectId;
	private readonly int _skillId;
	private readonly int _skillLevel;
	private readonly int _targetObjectId;

	public SmSummonUseSkill(int summonObjectId, int skillId, int skillLevel, int targetObjectId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_SUMMON_USESKILL(int summonId, int skillId, int skillLvl, int targetId).
		_summonObjectId = summonObjectId;
		_skillId = skillId;
		_skillLevel = skillLevel;
		_targetObjectId = targetObjectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_summonObjectId);
		buffer.WriteH(_skillId);
		buffer.WriteC(_skillLevel);
		buffer.WriteD(_targetObjectId);
	}
}
