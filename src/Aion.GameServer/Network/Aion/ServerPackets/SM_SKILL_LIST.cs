using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Skillinfo;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SKILL_LIST (MrPoke, ATracer, Neon). Sends the player's skill list (silent or with a new-skill notification message). Converges PlayerEnterWorldService SplitList paging. Collections.singletonList->new List; String.valueOf->ToString; SkillEntryWriter.writeSkillEntry(...,getBuf())->WriteSkillEntry(...,GetBuf()). PlayerSkillEntry/SkillEntryWriter red-tolerated.</summary>
public class SM_SKILL_LIST : AionServerPacket
{
    public const int STATIC_BODY_SIZE = 7;

    private readonly List<PlayerSkillEntry> skillList;
    private readonly int messageId;
    private string skillNameL10n;
    private string skillLvl;
    private bool silentUpdate = false;

    public SM_SKILL_LIST(List<PlayerSkillEntry> skillList)
    {
        this.skillList = skillList;
        this.messageId = 0;
        this.silentUpdate = true;
    }

    public SM_SKILL_LIST(PlayerSkillEntry skill, int messageId)
    {
        this.skillList = new List<PlayerSkillEntry> { skill };
        this.messageId = messageId;
        this.skillNameL10n = DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId()).GetL10n();
        this.skillLvl = skill.GetSkillLevel().ToString();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillList.Count); // skills list size
        WriteC(silentUpdate ? 1 : 0); // 1 only list in skill list, 0 list + update skill bar level and notify if new
        foreach (PlayerSkillEntry entry in skillList)
            SkillEntryWriter.WriteSkillEntry(entry, GetBuf());
        WriteD(messageId);
        if (messageId != 0)
        {
            WriteS(skillNameL10n);
            WriteS(skillLvl);
            WriteH(0x00);
        }
    }
}
