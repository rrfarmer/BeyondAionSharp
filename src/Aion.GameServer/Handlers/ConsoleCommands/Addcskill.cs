using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Addcskill (ginho1).</summary>
public class Addcskill : ConsoleCommand
{
    public Addcskill()
        : base("addcskill")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

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

        string skillName = paramsArr[0];
        int skillId = 0;

        FileInfo xml = new FileInfo("./data/handlers/consolecommands/data/skills.xml");
        SkillData data = JAXBUtil.Deserialize<SkillData>(xml);
        SkillTemplate skillTemplate = data.GetSkillTemplate(skillName);

        if (skillTemplate != null)
            skillId = skillTemplate.GetSkillId();

        if (skillId > 0)
        {
            player.GetSkillList().AddSkill(player, skillId, 1);
            PacketSendUtility.SendMessage(admin, "You have success add skill");
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
        public int skillId;

        [XmlAttribute("name")]
        public string name;

        public string GetName() => name;

        public int GetSkillId() => skillId;
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
