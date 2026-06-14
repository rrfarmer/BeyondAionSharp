using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AddSkill (Phantom).</summary>
public class AddSkill : AdminCommand
{
    public AddSkill()
        : base("addskill")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length != 2)
        {
            PacketSendUtility.SendMessage(player, "syntax //addskill <skillId> <skillLevel>");
            return;
        }

        VisibleObject target = player.GetTarget();

        int skillId = 0;
        int skillLevel = 0;

        if (!int.TryParse(paramsArr[0], out skillId) || !int.TryParse(paramsArr[1], out skillLevel))
        {
            PacketSendUtility.SendMessage(player, "Parameters need to be an integer.");
            return;
        }

        if (target is Player targetpl)
        {
            targetpl.GetSkillList().AddSkill(targetpl, skillId, skillLevel);
            PacketSendUtility.SendMessage(player, "You have success add skill");
            PacketSendUtility.SendMessage(targetpl, "You have acquire a new skill");
        }
    }
}
