using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/DelSkill (xTz).</summary>
public class DelSkill : AdminCommand
{
    public DelSkill()
        : base("delskill")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1 || paramsArr.Length > 2)
        {
            PacketSendUtility.SendMessage(admin, "No parameters detected.\n" + "Please use //delskill <Player name> <all | skillId>\n"
                + "or use //delskill [target] <all | skillId>");
            return;
        }

        Player player;
        PlayerSkillList playerSkillList = null;
        string recipient = null;
        recipient = Util.ConvertName(paramsArr[0]);
        int skillId = 0;
        if (paramsArr.Length == 2)
        {
            player = Aion.GameServer.World.World.GetInstance().GetPlayer(recipient);
            if (player == null)
            {
                PacketSendUtility.SendMessage(admin, "The specified player is not online.");
                return;
            }

            if ("all".StartsWith(paramsArr[1]))
                playerSkillList = player.GetSkillList();
            else
            {
                if (!int.TryParse(paramsArr[1], out skillId))
                {
                    PacketSendUtility.SendMessage(admin, "Param 1 must be an integer or <all>.");
                    return;
                }

                if (!Check(admin, player, skillId))
                    return;
            }
            Apply(admin, player, skillId, playerSkillList);
        }
        if (paramsArr.Length == 1)
        {
            VisibleObject target = admin.GetTarget();
            if (target == null)
            {
                PacketSendUtility.SendMessage(admin, "You should select a target first!");
                return;
            }

            if (target is Player)
            {
                player = (Player)target;

                if ("all".StartsWith(paramsArr[0]))
                    playerSkillList = player.GetSkillList();
                else
                {
                    if (!int.TryParse(paramsArr[0], out skillId))
                    {
                        PacketSendUtility.SendMessage(admin, "Param 0 must be an integer or <all>.");
                        return;
                    }

                    if (!Check(admin, player, skillId))
                        return;
                }
                if (target is Player)
                    Apply(admin, player, skillId, playerSkillList);
            }
            else
                PacketSendUtility.SendMessage(admin, "This command can only be used on a player !");
        }
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

    private void Apply(Player admin, Player player, int skillId, PlayerSkillList playerSkillList)
    {
        if (skillId != 0)
        {
            SkillLearnService.RemoveSkill(player, skillId);
            PacketSendUtility.SendMessage(admin, "You have successfully deleted the specified skill.");
        }
        else
        {
            foreach (PlayerSkillEntry skillEntry in playerSkillList.GetAllSkills())
            {
                if (!skillEntry.IsStigmaSkill())
                {
                    SkillLearnService.RemoveSkill(player, skillEntry.GetSkillId());
                }
            }

            PacketSendUtility.SendMessage(admin, "You have success delete All skills.");
        }
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "No parameters detected.\n" + "Please use //delskill <Player name> <all | skillId>\n"
            + "or use //delskill [target] <all | skillId>");
    }
}
