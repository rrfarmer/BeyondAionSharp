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

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length != 2)
        {
            PacketSendUtility.SendMessage(player, "syntax //addskill <skillId> <skillLevel>");
            return;
        }

        Player target = player.GetTarget() is Player p ? p : player;

        int skillId = 0;
        int skillLevel = 0;

        if (!TryParseInt(paramsArr[0], out skillId) || !TryParseInt(paramsArr[1], out skillLevel))
        {
            PacketSendUtility.SendMessage(player, "Parameters need to be an integer.");
            return;
        }

        target.GetSkillList().AddSkill(target, skillId, skillLevel);
        PacketSendUtility.SendMessage(player, "You have success add skill");
        if (!target.Equals(player))
            PacketSendUtility.SendMessage(target, "You have acquire a new skill");
    }
}
