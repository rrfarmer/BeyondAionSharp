using System.Collections.Generic;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Skillinfo;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SHOW_PLAYER_SKILLS (Yeats). Sends a player's skill list to a GM. getBuf()->GetBuf(); PlayerSkillEntry/SkillEntryWriter red-tolerated.</summary>
public class SM_GM_SHOW_PLAYER_SKILLS : AionServerPacket
{
    public const int STATIC_BODY_SIZE = 2;

    private readonly List<PlayerSkillEntry> skillList;

    public SM_GM_SHOW_PLAYER_SKILLS(List<PlayerSkillEntry> skillList)
    {
        this.skillList = skillList;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillList.Count); // skills list size
        foreach (PlayerSkillEntry entry in skillList)
            SkillEntryWriter.WriteSkillEntry(entry, GetBuf());
    }
}
