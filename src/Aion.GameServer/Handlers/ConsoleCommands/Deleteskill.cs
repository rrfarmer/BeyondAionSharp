using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Deleteskill (ginho1).</summary>
public class Deleteskill : ConsoleCommand
{
    public Deleteskill()
        : base("deleteskill")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        string skillName = paramsArr[0];
        int skillId = 0;

        VisibleObject target = admin.GetTarget();
        if (target == null)
        {
            PacketSendUtility.SendMessage(admin, "No target selected.");
            return;
        }

        if (!(target is Player))
        {
            PacketSendUtility.SendMessage(admin, "This command can only be used on a player!");
            return;
        }

        Player player = (Player)target;
        FileInfo xml = new FileInfo("./data/handlers/consolecommands/data/skills.xml");
        SkillData data = JAXBUtil.Deserialize<SkillData>(xml);
        SkillTemplate skillTemplate = data.GetSkillTemplate(skillName);

        if (skillTemplate != null)
            skillId = skillTemplate.GetTemplateId();

        if (skillId > 0 && Check(admin, player, skillId))
            Apply(admin, player, skillId);
    }

    private static bool Check(Player admin, Player player, int skillId)
    {
        if (skillId != 0 && !player.GetSkillList().IsSkillPresent(skillId))
        {
            PacketSendUtility.SendMessage(admin, "Player dont have this skill.");
            return false;
        }
        if (player.GetSkillList().GetSkillEntry(skillId).IsStigmaSkill())
        {
            PacketSendUtility.SendMessage(admin, "You can't remove stigma skill.");
            return false;
        }
        return true;
    }

    public void Apply(Player admin, Player player, int skillId)
    {
        if (skillId != 0)
        {
            SkillLearnService.RemoveSkill(player, skillId);
            PacketSendUtility.SendMessage(admin, "You have successfully deleted the specified skill.");
        }
    }

    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///addcskill <skill name>");
    }

    [XmlRoot("skill")]
    public class SkillTemplate
    {
        [XmlAttribute("id")]
        public string id;

        [XmlAttribute("name")]
        public string name;

        public string GetName() => name;

        // Java parity: afterUnmarshal parsed the @XmlID String id into an int.
        public int GetTemplateId() => int.Parse(id);
    }

    [XmlRoot("skills")]
    public class SkillData
    {
        [XmlElement("skill")]
        public List<SkillTemplate> its;

        public SkillTemplate GetSkillTemplate(string skill)
        {
            foreach (SkillTemplate it in GetData())
            {
                if (it.GetName().ToLower().Equals(skill.ToLower()))
                    return it;
            }
            return null;
        }

        protected List<SkillTemplate> GetData() => its;
    }
}
