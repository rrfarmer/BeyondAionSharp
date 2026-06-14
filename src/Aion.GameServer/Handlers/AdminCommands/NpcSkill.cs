using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/NpcSkill (Wakizashi). The Java info(Player,String) override is an empty TODO stub that is never called, so it is omitted.</summary>
public class NpcSkill : AdminCommand
{
    public NpcSkill()
        : base("npcskill")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        Npc target = null;
        VisibleObject creature = admin.GetTarget();
        if (admin.GetTarget() is Npc)
        {
            target = (Npc)creature;
        }

        if (target == null)
        {
            PacketSendUtility.SendMessage(admin, "You should select a valid target first!");
            return;
        }

        System.Text.StringBuilder strbld = new System.Text.StringBuilder("-list of skills:\n");

        List<NpcSkillTemplate> list = null;
        if (DataManager.NPC_SKILL_DATA.GetNpcSkillList(target.GetNpcId()) != null)
        {
            list = DataManager.NPC_SKILL_DATA.GetNpcSkillList(target.GetNpcId()).GetNpcSkills();
        }

        if (list != null && list.Count != 0)
        {
            foreach (NpcSkillTemplate skill in list)
                strbld.Append("    level " + skill.GetSkillLevel() + " of " + skill.GetSkillId() + ", " + skill.GetProbability() + "% prob and " + skill.GetCooldown() + "ms cd.\n");
            ShowAllLines(admin, strbld.ToString());
        }
        else
        {
            PacketSendUtility.SendMessage(admin, "This npc does not have any skills.");
        }
    }

    private void ShowAllLines(Player admin, string str)
    {
        int index = 0;
        string[] strarray = str.Split('\n');

        while (index < strarray.Length - 20)
        {
            System.Text.StringBuilder strbld = new System.Text.StringBuilder();
            for (int i = 0; i < 20; i++, index++)
            {
                strbld.Append(strarray[index]);
                if (i < 20 - 1)
                    strbld.Append("\n");
            }
            PacketSendUtility.SendMessage(admin, strbld.ToString());
        }
        int odd = strarray.Length - index;
        System.Text.StringBuilder strbld2 = new System.Text.StringBuilder();
        for (int i = 0; i < odd; i++, index++)
            strbld2.Append(strarray[index] + "\n");
        PacketSendUtility.SendMessage(admin, strbld2.ToString());
    }
}
