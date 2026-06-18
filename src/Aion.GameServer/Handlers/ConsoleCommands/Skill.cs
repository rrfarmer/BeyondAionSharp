using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.Aion.Skillinfo;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Skill (Yeats).</summary>
public class Skill : ConsoleCommand
{
    public Skill()
        : base("skill")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        Player target = null;
        if (paramsArr.Length > 0)
            target = Aion.GameServer.World.World.GetInstance().GetPlayer(paramsArr[0]);
        if (target == null && admin.GetTarget() is Player player)
            target = player;
        if (target != null)
        {
            SplitList<PlayerSkillEntry> skillEntrySplitList = new DynamicServerPacketBodySplitList<PlayerSkillEntry>(target.GetSkillList().GetAllSkills(), false,
                SM_GM_SHOW_PLAYER_SKILLS.STATIC_BODY_SIZE, SkillEntryWriter.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
            foreach (var part in skillEntrySplitList)
                PacketSendUtility.SendPacket(admin, new SM_GM_SHOW_PLAYER_SKILLS(part));
        }
    }
}
