using System;
using Aion.Commons.Nio;
using Aion.GameServer.Model.Skill;

namespace Aion.GameServer.Network.Aion.Skillinfo;

/// <summary>
/// Java parity: network/aion/skillinfo/SkillEntryWriter. Buffer-writer for a single PlayerSkillEntry, extending PacketWriteHelper.
/// Function&lt;PlayerSkillEntry, Integer&gt; -> Func&lt;PlayerSkillEntry, int&gt;. PlayerSkillEntry red-tolerated.
/// </summary>
public class SkillEntryWriter : global::Aion.GameServer.Network.PacketWriteHelper
{
    public static readonly Func<PlayerSkillEntry, int> DYNAMIC_BODY_PART_SIZE_CALCULATOR = skill => 11;

    private readonly PlayerSkillEntry skillEntry;

    public static void WriteSkillEntry(PlayerSkillEntry skillEntry, ByteBuffer buffer)
    {
        SkillEntryWriter entryWriter = new SkillEntryWriter(skillEntry);
        entryWriter.WriteMe(buffer);
    }

    public SkillEntryWriter(PlayerSkillEntry skillEntry)
    {
        this.skillEntry = skillEntry;
    }

    public override void WriteMe(ByteBuffer buf)
    {
        WriteH(buf, skillEntry.GetSkillId());
        WriteH(buf, skillEntry.IsNormalSkill() ? 1 : skillEntry.GetSkillLevel());
        WriteC(buf, 0x00);
        WriteC(buf, skillEntry.GetProfessionSkillBarSize());
        WriteD(buf, skillEntry.IsProfessionSkill() ? skillEntry.GetProfessionFlag() : skillEntry.GetFlag());
        WriteC(buf, skillEntry.GetSkillType()); // 0 normal skill , 1 stigma skill , 3 linked stigma skill
    }
}
