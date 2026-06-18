using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/PlayerInfo (lyahim, antness). Shows information about a player.</summary>
public class PlayerInfo : AdminCommand
{
    public PlayerInfo()
        : base("playerinfo", "Shows information about a player.")
    {
        SetSyntaxInfo("<player name> <loc|item|group|skills|legion|ap|chars|knownlist>");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        string playerName = Util.ConvertName(paramsArr[0]);
        Player target = World.World.GetInstance().GetPlayer(playerName);
        if (target == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }

        SendInfo(admin,
            "\n[Info about " + target.GetName() + "]\n-common: lv" + target.GetLevel() + "(" + target.GetCommonData().GetExpShown() + " xp), "
                + target.GetRace() + ", " + target.GetPlayerClass() + "\n-ip: " + target.GetClientConnection().GetIP() + "\n" + "-account name: "
                + target.GetClientConnection().GetAccount().GetName() + "\n");

        if (paramsArr.Length < 2)
            return;

        if (paramsArr[1].Equals("item"))
        {
            StringBuilder strbld = new StringBuilder("-items in inventory:");
            AppendItems(strbld, target.GetInventory().GetItemsWithKinah());
            strbld.Append("-equipped items:");
            AppendItems(strbld, target.GetEquipment().GetEquippedItems());
            strbld.Append("-items in warehouse:");
            AppendItems(strbld, target.GetWarehouse().GetItemsWithKinah());
            SendInfo(admin, strbld.ToString());
        }
        else if (paramsArr[1].Equals("group"))
        {
            StringBuilder strbld = new StringBuilder("-group info:\n\tLeader: ");

            PlayerGroup group = target.GetPlayerGroup();
            if (group == null)
                SendInfo(admin, "-group info: no group");
            else
            {
                strbld.Append(group.GetLeader().GetName() + "\n\tMembers:\n");
                group.ForEach(player => strbld.Append("\t\t" + player.GetName() + "\n"));
                SendInfo(admin, strbld.ToString());
            }
        }
        else if (paramsArr[1].Equals("skills"))
        {
            StringBuilder strbld = new StringBuilder("-list of skills:\n");
            foreach (PlayerSkillEntry skill in target.GetSkillList().GetAllSkills())
                strbld.Append("\tlevel " + skill.GetSkillLevel() + " of " + DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId()).GetName() + "\n");
            SendInfo(admin, strbld.ToString());
        }
        else if (paramsArr[1].Equals("loc"))
        {
            string chatLink = ChatUtil.Position(target.GetName(), target.GetPosition());
            SendInfo(admin, "- " + chatLink + "'s location:\n\t" + target.GetPosition().ToCoordString());
        }
        else if (paramsArr[1].Equals("legion"))
        {
            Legion legion = target.GetLegion();
            if (legion == null)
                SendInfo(admin, "-legion info: no legion");
            else
            {
                StringBuilder strbld = new StringBuilder();
                strbld.Append("-legion info:\n\tname: " + legion.GetName() + ", level: " + legion.GetLegionLevel() + "\n\tmembers(online):\n");
                foreach (LegionMember lm in legion.GetMembers())
                    strbld.Append("\t\t" + lm.GetName() + "(" + (lm.IsOnline() ? "online" : "offline") + ")" + lm.GetRank().ToString() + "\n");
                SendInfo(admin, strbld.ToString());
            }
        }
        else if (paramsArr[1].Equals("ap"))
        {
            SendInfo(admin, "AP info about " + target.GetName());
            SendInfo(admin, "Total AP = " + target.GetAbyssRank().GetAp());
            SendInfo(admin, "Total Kills = " + target.GetAbyssRank().GetAllKill());
            SendInfo(admin, "Today Kills = " + target.GetAbyssRank().GetDailyKill());
            SendInfo(admin, "Today AP = " + target.GetAbyssRank().GetDailyAP());
        }
        else if (paramsArr[1].Equals("chars"))
        {
            SendInfo(admin, "Others characters of " + target.GetName() + " (" + target.GetClientConnection().GetAccount().Size() + ") :");
            foreach (var d in target.GetClientConnection().GetAccount())
                SendInfo(admin, d.GetPlayerCommonData().GetName());
        }
        else if (paramsArr[1].Equals("knownlist"))
        {
            SendInfo(admin, "KnownList of " + target.GetName() + string.Concat(target.GetKnownList().Stream().Select(o => "\n\t" + o)));
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void AppendItems(StringBuilder strbld, List<Item> items)
    {
        if (items.Count == 0)
            strbld.Append("\nnone");
        else
            foreach (Item item in items)
                strbld.Append("\n\t" + item.GetItemCount() + "x " + ChatUtil.Item(item.GetItemId()));
    }
}
