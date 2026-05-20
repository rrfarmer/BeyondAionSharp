using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSkillList : GameServerPacket
{
	public const int PacketOpCode = 44;

	private readonly IReadOnlyList<PlayerSkill> _skills;
	private readonly int _messageId;
	private readonly Func<DateTimeOffset> _clock;

	public SmSkillList(IReadOnlyList<PlayerSkill> skills, Func<DateTimeOffset>? clock = null)
		: this(skills, messageId: 0, clock)
	{
	}

	public SmSkillList(IReadOnlyList<PlayerSkill> skills, int messageId, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		_skills = skills;
		_messageId = messageId;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SKILL_LIST.writeImpl.
		buffer.WriteH(_skills.Count);
		buffer.WriteC(_messageId == 0 ? 1 : 0);
		var now = _clock();
		foreach (var skill in _skills)
			WriteSkillEntry(buffer, skill, now);
		buffer.WriteD(_messageId);
	}

	private static void WriteSkillEntry(PacketBuffer buffer, PlayerSkill skill, DateTimeOffset now)
	{
		// Java parity: network/aion/skillinfo/SkillEntryWriter.writeMe.
		buffer.WriteH(skill.SkillId);
		buffer.WriteH(skill.GetClientSkillLevel());
		buffer.WriteC(0);
		buffer.WriteC(skill.GetProfessionSkillBarSize());
		buffer.WriteD(skill.GetClientFlag(now));
		buffer.WriteC(skill.SkillType);
	}
}
